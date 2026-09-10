using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TremauxLock
{
    internal sealed class HideResult
    {
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
    }

    internal sealed class ShowResult
    {
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public string? BackupWarning { get; init; }
    }

    /// <summary>
    /// Modo "Ocultar rapido": apenas move private para private.hidden (operacao
    /// instantanea de metadados, sem copiar bytes) e protege a volta com uma
    /// senha simples. Esconde de curiosos — NAO é criptografia.
    /// </summary>
    internal sealed class QuickHideService
    {
        public const int MinimumPasswordLength = 4;
        public const int MaxPasswordHintLength = HideMetadata.MaxPasswordHintLength;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private static readonly JsonSerializerOptions MetadataJsonOptions = new() { WriteIndented = true };
        private static readonly UTF8Encoding Utf8NoBom = new(false, true);

        public QuickHideService(string applicationDirectory)
        {
            ApplicationDirectory = applicationDirectory;
            WorkingFolderPath = Path.Combine(applicationDirectory, "private");
            HiddenFolderPath = Path.Combine(applicationDirectory, "private.hidden");
            HideMetadataPath = Path.Combine(applicationDirectory, "private.hide.json");
        }

        public string ApplicationDirectory { get; }

        public string WorkingFolderPath { get; }

        public string HiddenFolderPath { get; }

        public string HideMetadataPath { get; }

        public Task<HideResult> HideAsync(string password, string? passwordHint = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Hide(password, passwordHint, cancellationToken), cancellationToken);
        }

        public Task<ShowResult> ShowAsync(string password, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Show(password, cancellationToken), cancellationToken);
        }

        public HideResult Hide(string password, string? passwordHint = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
            {
                throw new InvalidOperationException($"Use uma senha simples com pelo menos {MinimumPasswordLength} caracteres.");
            }

            string normalizedHint = NormalizeAndValidateHint(password, passwordHint);

            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(WorkingFolderPath))
            {
                throw new InvalidOperationException("A pasta private nao existe. Nao ha nada para ocultar.");
            }

            if (Directory.Exists(HiddenFolderPath) || File.Exists(HideMetadataPath))
            {
                throw new InvalidOperationException("Ja existe uma pasta oculta. Mostre o conteudo antes de ocultar de novo.");
            }

            if (Directory.Exists(Path.Combine(ApplicationDirectory, "private.locked"))
                || File.Exists(Path.Combine(ApplicationDirectory, "private.vault.json")))
            {
                throw new InvalidOperationException("A protecao por criptografia esta ativa. Restaure a pasta antes de ocultar.");
            }

            string[] files = EnumerateFiles(WorkingFolderPath);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("A pasta private esta vazia. Adicione arquivos antes de ocultar.");
            }

            long totalBytes = files.Sum(file => new FileInfo(file).Length);

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash;
            try
            {
                hash = HashPassword(password, salt);
            }
            catch
            {
                CryptographicOperations.ZeroMemory(salt);
                throw;
            }

            var metadata = new HideMetadata
            {
                HiddenUtc = DateTime.UtcNow.ToString("O"),
                FileCount = files.Length,
                TotalBytes = totalBytes,
                PasswordSaltBase64 = Convert.ToBase64String(salt),
                PasswordHashBase64 = Convert.ToBase64String(hash),
                PasswordHint = normalizedHint
            };
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);

            string tempMetadataPath = HideMetadataPath + ".pending";
            if (File.Exists(tempMetadataPath))
            {
                File.Delete(tempMetadataPath);
            }

            WriteMetadataFile(tempMetadataPath, metadata);

            bool movedFolder = false;
            try
            {
                Directory.Move(WorkingFolderPath, HiddenFolderPath);
                movedFolder = true;
                File.Move(tempMetadataPath, HideMetadataPath);
                ApplyHiddenPresentation();
                VaultSecurity.TryHardenPath(HiddenFolderPath);
                VaultSecurity.TryHardenPath(HideMetadataPath);
            }
            catch
            {
                TryDeleteFile(tempMetadataPath);
                if (movedFolder && Directory.Exists(HiddenFolderPath) && !Directory.Exists(WorkingFolderPath))
                {
                    try
                    {
                        Directory.Move(HiddenFolderPath, WorkingFolderPath);
                    }
                    catch
                    {
                    }
                }

                throw;
            }

            return new HideResult { FileCount = files.Length, TotalBytes = totalBytes };
        }

        public ShowResult Show(string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new VaultAuthenticationException("Senha incorreta.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(WorkingFolderPath))
            {
                throw new InvalidOperationException("A pasta private ja existe. Mova ou remova a pasta atual antes de mostrar.");
            }

            if (!Directory.Exists(HiddenFolderPath) || !File.Exists(HideMetadataPath))
            {
                throw new InvalidOperationException("Nao existe pasta oculta para mostrar.");
            }

            HideMetadata metadata = LoadMetadata();
            VerifyPassword(password, metadata);

            string[] files = EnumerateFiles(HiddenFolderPath);
            if (files.Length != metadata.FileCount)
            {
                throw new VaultIntegrityException("A quantidade de arquivos ocultos nao bate com os metadados.");
            }

            RemoveHiddenPresentation();

            Directory.Move(HiddenFolderPath, WorkingFolderPath);
            ClearHiddenAttributes(WorkingFolderPath);
            VaultSecurity.TryHardenPath(WorkingFolderPath);

            string? backupWarning = null;
            try
            {
                File.Delete(HideMetadataPath);
            }
            catch (Exception ex)
            {
                backupWarning = $"O conteudo foi mostrado, mas o arquivo '{HideMetadataPath}' nao foi removido automaticamente: {ex.Message}";
            }

            return new ShowResult
            {
                FileCount = metadata.FileCount,
                TotalBytes = metadata.TotalBytes,
                BackupWarning = backupWarning
            };
        }

        /// <summary>
        /// Le a dica de senha em claro sem exigir a senha. Retorna null quando
        /// nao ha pasta oculta, nao ha dica cadastrada ou os metadados estao invalidos.
        /// A dica nunca permite recuperar a senha — serve so como lembrete.
        /// </summary>
        public string? TryGetPasswordHint()
        {
            try
            {
                if (!File.Exists(HideMetadataPath))
                {
                    return null;
                }

                HideMetadata metadata = LoadMetadata();
                string hint = (metadata.PasswordHint ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(hint) ? null : hint;
            }
            catch
            {
                return null;
            }
        }

        public static string NormalizeAndValidateHint(string password, string? hint)
        {
            string normalized = (hint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.Length > MaxPasswordHintLength)
            {
                throw new InvalidOperationException($"A dica pode ter no maximo {MaxPasswordHintLength} caracteres.");
            }

            if (normalized.Equals(password, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A dica nao pode ser igual a senha.");
            }

            if (password.Length >= MinimumPasswordLength
                && normalized.IndexOf(password, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("A dica nao pode conter a propria senha.");
            }

            return normalized;
        }

        internal HideMetadata LoadMetadata()
        {
            HideMetadata? metadata;
            try
            {
                string json = File.ReadAllText(HideMetadataPath, Utf8NoBom);
                metadata = JsonSerializer.Deserialize<HideMetadata>(json);
            }
            catch (JsonException ex)
            {
                throw new VaultIntegrityException("Nao foi possivel ler os metadados da ocultacao.", ex);
            }
            catch (NotSupportedException ex)
            {
                throw new VaultIntegrityException("Nao foi possivel ler os metadados da ocultacao.", ex);
            }

            if (metadata == null)
            {
                throw new VaultIntegrityException("Nao foi possivel ler os metadados da ocultacao.");
            }

            ValidateMetadata(metadata);
            return metadata;
        }

        private static void ValidateMetadata(HideMetadata metadata)
        {
            if (metadata.Version != HideMetadata.CurrentVersion)
            {
                throw new VaultIntegrityException("Versao de metadados da ocultacao nao suportada.");
            }

            if (metadata.HiddenFolderName != "private.hidden")
            {
                throw new VaultIntegrityException("Os metadados da ocultacao apontam para uma pasta inesperada.");
            }

            if (metadata.FileCount <= 0 || metadata.TotalBytes < 0)
            {
                throw new VaultIntegrityException("Os metadados da ocultacao contem contagem ou tamanho invalido.");
            }

            if (!DateTimeOffset.TryParse(metadata.HiddenUtc, out _))
            {
                throw new VaultIntegrityException("Os metadados da ocultacao contem data invalida.");
            }

            byte[] salt = DecodeBase64Field(metadata.PasswordSaltBase64, SaltSize);
            byte[] hash = DecodeBase64Field(metadata.PasswordHashBase64, HashSize);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);

            // Dica e opcional e fica em claro. Arquivos antigos nao tem o campo.
            metadata.PasswordHint = (metadata.PasswordHint ?? string.Empty).Trim();
            if (metadata.PasswordHint.Length > MaxPasswordHintLength)
            {
                throw new VaultIntegrityException("Os metadados da ocultacao contem dica invalida.");
            }
        }

        private static byte[] DecodeBase64Field(string? value, int expectedLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new VaultIntegrityException("Os metadados da ocultacao estao incompletos.");
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(value);
            }
            catch (FormatException ex)
            {
                throw new VaultIntegrityException("Os metadados da ocultacao estao invalidos.", ex);
            }

            if (decoded.Length != expectedLength)
            {
                CryptographicOperations.ZeroMemory(decoded);
                throw new VaultIntegrityException("Os metadados da ocultacao estao invalidos.");
            }

            return decoded;
        }

        private static void VerifyPassword(string password, HideMetadata metadata)
        {
            byte[] salt = DecodeBase64Field(metadata.PasswordSaltBase64, SaltSize);
            byte[] expectedHash = DecodeBase64Field(metadata.PasswordHashBase64, HashSize);
            byte[] actualHash;
            try
            {
                actualHash = HashPassword(password, salt);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
            }

            try
            {
                if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                {
                    throw new VaultAuthenticationException("Senha incorreta.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualHash);
                CryptographicOperations.ZeroMemory(expectedHash);
            }
        }

        private static byte[] HashPassword(string password, byte[] salt)
        {
            byte[] passwordBytes = Utf8NoBom.GetBytes(password);
            byte[] combined = new byte[salt.Length + passwordBytes.Length];
            try
            {
                Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
                Buffer.BlockCopy(passwordBytes, 0, combined, salt.Length, passwordBytes.Length);
                return SHA256.HashData(combined);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
                CryptographicOperations.ZeroMemory(combined);
            }
        }

        private static void WriteMetadataFile(string metadataPath, HideMetadata metadata)
        {
            string json = JsonSerializer.Serialize(metadata, MetadataJsonOptions);
            File.WriteAllText(metadataPath, json, Utf8NoBom);
        }

        private static string[] EnumerateFiles(string rootPath)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            return Directory.EnumerateFiles(rootPath, "*", options)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private void ApplyHiddenPresentation()
        {
            try
            {
                if (Directory.Exists(HiddenFolderPath))
                {
                    FileAttributes attributes = File.GetAttributes(HiddenFolderPath);
                    attributes |= FileAttributes.Hidden | FileAttributes.System;
                    File.SetAttributes(HiddenFolderPath, attributes);
                }
            }
            catch
            {
            }

            try
            {
                if (File.Exists(HideMetadataPath))
                {
                    FileAttributes attributes = File.GetAttributes(HideMetadataPath);
                    attributes |= FileAttributes.Hidden | FileAttributes.System;
                    File.SetAttributes(HideMetadataPath, attributes);
                }
            }
            catch
            {
            }
        }

        private void RemoveHiddenPresentation()
        {
            try
            {
                if (File.Exists(HideMetadataPath))
                {
                    FileAttributes attributes = File.GetAttributes(HideMetadataPath);
                    attributes &= ~FileAttributes.Hidden;
                    attributes &= ~FileAttributes.System;
                    File.SetAttributes(HideMetadataPath, attributes);
                }
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(HiddenFolderPath))
                {
                    FileAttributes attributes = File.GetAttributes(HiddenFolderPath);
                    attributes &= ~FileAttributes.Hidden;
                    attributes &= ~FileAttributes.System;
                    File.SetAttributes(HiddenFolderPath, attributes);
                }
            }
            catch
            {
            }
        }

        private static void ClearHiddenAttributes(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    attributes &= ~FileAttributes.Hidden;
                    attributes &= ~FileAttributes.System;
                    File.SetAttributes(path, attributes);
                }
            }
            catch
            {
            }
        }
    }
}
