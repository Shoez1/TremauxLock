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
    internal enum VaultState
    {
        Empty,
        Unlocked,
        Locked,
        Inconsistent
    }

    internal sealed class VaultProgress
    {
        public string Step { get; init; } = string.Empty;
        public int Current { get; init; }
        public int Total { get; init; }
    }

    internal sealed class VaultOverview
    {
        public VaultState State { get; init; }
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public string WorkingFolderPath { get; init; } = string.Empty;
        public string LockedFolderPath { get; init; } = string.Empty;
        public string MetadataPath { get; init; } = string.Empty;
        public DateTimeOffset? LockedAtUtc { get; init; }
    }

    internal sealed class LockResult
    {
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public string RecoveryKey { get; init; } = string.Empty;
        public string? BackupWarning { get; init; }
    }

    internal sealed class UnlockResult
    {
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public string? BackupWarning { get; init; }
    }

    internal sealed class VaultAuthenticationException : Exception
    {
        public VaultAuthenticationException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class VaultIntegrityException : Exception
    {
        public VaultIntegrityException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    internal sealed class VaultService
    {
        private const string LockedExtension = ".tmx";
        private const int WipeBufferSize = 1024 * 1024;
        private static readonly JsonSerializerOptions MetadataJsonOptions = new() { WriteIndented = true };
        private static readonly UTF8Encoding Utf8NoBom = new(false, true);

        public VaultService(string applicationDirectory)
        {
            ApplicationDirectory = applicationDirectory;
            WorkingFolderPath = Path.Combine(applicationDirectory, "private");
            LockedFolderPath = Path.Combine(applicationDirectory, "private.locked");
            MetadataPath = Path.Combine(applicationDirectory, "private.vault.json");
        }

        public string ApplicationDirectory { get; }

        public string WorkingFolderPath { get; }

        public string LockedFolderPath { get; }

        public string MetadataPath { get; }

        public string? RecoveryNotice { get; private set; }

        public void EnsureWorkspace()
        {
            RecoverInterruptedOperations();

            if (!Directory.Exists(WorkingFolderPath) && !Directory.Exists(LockedFolderPath))
            {
                Directory.CreateDirectory(WorkingFolderPath);
            }

            TryHardenKnownVaultPaths();
        }

        private void RecoverInterruptedOperations()
        {
            RecoveryNotice = null;

            bool hasPrivate = Directory.Exists(WorkingFolderPath);
            bool hasLocked = Directory.Exists(LockedFolderPath);
            bool hasMetadata = File.Exists(MetadataPath);
            bool metadataIsValid = hasMetadata && CanReadMetadata();
            string pendingWorking = WorkingFolderPath + ".pending";
            string pendingLocked = LockedFolderPath + ".pending";
            string pendingMetadata = MetadataPath + ".pending";
            string[] plaintextBackups = FindBackupDirectories("private.plain.backup");
            string[] encryptedBackups = FindBackupDirectories("private.locked.encrypted.backup");
            var notices = new List<string>();

            if (hasPrivate)
            {
                if (Directory.Exists(pendingWorking))
                {
                    TryWipePlaintextDirectory(pendingWorking);
                    notices.Add("Uma pasta temporária em claro de uma operação interrompida foi removida.");
                }

                TryCleanupDirectory(pendingLocked);
                TryCleanupFile(pendingMetadata);

                if (hasLocked && metadataIsValid)
                {
                    foreach (string backup in plaintextBackups)
                    {
                        TryWipePlaintextDirectory(backup);
                        notices.Add("Resíduos em claro de um bloqueio anterior foram removidos.");
                    }

                    foreach (string backup in encryptedBackups)
                    {
                        TryCleanupDirectory(backup);
                    }
                }
                else if (plaintextBackups.Length > 0)
                {
                    notices.Add("Há uma cópia em claro de uma operação anterior. Verifique as pastas private.plain.backup antes de continuar.");
                }
            }
            else if (hasLocked && metadataIsValid)
            {
                if (Directory.Exists(pendingWorking))
                {
                    TryWipePlaintextDirectory(pendingWorking);
                    notices.Add("Uma pasta temporária em claro de um desbloqueio interrompido foi removida.");
                }

                TryCleanupDirectory(pendingLocked);
                TryCleanupFile(pendingMetadata);

                foreach (string backup in plaintextBackups)
                {
                    TryWipePlaintextDirectory(backup);
                    notices.Add("Resíduos em claro de um bloqueio anterior foram removidos.");
                }

                foreach (string backup in encryptedBackups)
                {
                    TryCleanupDirectory(backup);
                }
            }
            else if (hasLocked && !hasMetadata)
            {
                if (plaintextBackups.Length > 0)
                {
                    if (TryRestorePlaintextBackup(plaintextBackups[0], notices))
                    {
                        TryCleanupDirectory(LockedFolderPath);
                        TryCleanupDirectory(pendingLocked);
                        TryCleanupFile(pendingMetadata);

                        foreach (string duplicate in plaintextBackups.Skip(1))
                        {
                            TryWipePlaintextDirectory(duplicate);
                        }

                        notices.Add("Bloqueio interrompido: os arquivos em claro foram restaurados.");
                    }
                }
                else
                {
                    notices.Add("O cofre bloqueado não possui metadados válidos. Verifique a pasta private.locked.");
                }
            }
            else if (!hasLocked && metadataIsValid)
            {
                if (Directory.Exists(pendingWorking) && encryptedBackups.Length > 0)
                {
                    try
                    {
                        Directory.Move(pendingWorking, WorkingFolderPath);
                        TryCleanupFile(MetadataPath);

                        foreach (string backup in encryptedBackups)
                        {
                            TryCleanupDirectory(backup);
                        }

                        TryCleanupDirectory(pendingLocked);
                        TryCleanupFile(pendingMetadata);
                        notices.Add("Desbloqueio interrompido: os arquivos em claro foram restaurados.");
                    }
                    catch (Exception ex)
                    {
                        notices.Add($"Não foi possível concluir a recuperação automática: {ex.Message}");
                    }
                }
                else if (Directory.Exists(pendingWorking))
                {
                    TryWipePlaintextDirectory(pendingWorking);
                    notices.Add("Uma pasta temporária em claro incompleta foi removida.");
                }
                else
                {
                    notices.Add("Há metadados sem a pasta private.locked. Verifique a pasta da aplicação.");
                }
            }
            else if (!hasLocked && !hasMetadata)
            {
                if (plaintextBackups.Length > 0)
                {
                    if (TryRestorePlaintextBackup(plaintextBackups[0], notices))
                    {
                        foreach (string duplicate in plaintextBackups.Skip(1))
                        {
                            TryWipePlaintextDirectory(duplicate);
                        }

                        notices.Add("Bloqueio interrompido: os arquivos em claro foram restaurados.");
                    }
                }
                else if (Directory.Exists(pendingWorking))
                {
                    try
                    {
                        Directory.Move(pendingWorking, WorkingFolderPath);
                        notices.Add("Uma pasta temporária de desbloqueio foi promovida à pasta private.");
                    }
                    catch (Exception ex)
                    {
                        notices.Add($"Não foi possível recuperar a pasta temporária: {ex.Message}");
                    }
                }

                TryCleanupDirectory(pendingLocked);
                TryCleanupFile(pendingMetadata);
            }
            else
            {
                notices.Add("As pastas do cofre estão em conflito. Verifique private, private.locked e private.vault.json.");
            }

            if (notices.Count > 0)
            {
                RecoveryNotice = string.Join(Environment.NewLine, notices.Distinct());
            }
        }

        private bool CanReadMetadata()
        {
            try
            {
                LoadMetadata();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string[] FindBackupDirectories(string prefix)
        {
            try
            {
                return Directory.EnumerateDirectories(ApplicationDirectory, prefix + "*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private bool TryRestorePlaintextBackup(string backupPath, List<string> notices)
        {
            try
            {
                if (Directory.Exists(WorkingFolderPath))
                {
                    notices.Add("A pasta private já existe; a cópia em claro foi preservada para revisão manual.");
                    return false;
                }

                Directory.Move(backupPath, WorkingFolderPath);
                return true;
            }
            catch (Exception ex)
            {
                notices.Add($"Não foi possível restaurar a cópia em claro '{backupPath}': {ex.Message}");
                return false;
            }
        }

        private static void TryWipePlaintextDirectory(string path)
        {
            try
            {
                DeletePlaintextDirectory(path);
            }
            catch
            {
            }
        }

        public VaultOverview GetOverview()
        {
            bool hasUnlockedFolder = Directory.Exists(WorkingFolderPath);
            bool hasLockedFolder = Directory.Exists(LockedFolderPath);
            bool hasMetadata = File.Exists(MetadataPath);

            if (!hasUnlockedFolder && !hasLockedFolder && !hasMetadata)
            {
                Directory.CreateDirectory(WorkingFolderPath);
                VaultSecurity.TryHardenPath(WorkingFolderPath);
                hasUnlockedFolder = true;
            }

            if (hasUnlockedFolder && !hasLockedFolder)
            {
                string[] files = EnumerateVaultFiles(WorkingFolderPath);
                long totalBytes = GetTotalBytesSafe(files);

                // Metadados remanescentes com uma pasta private preenchida indicam que a
                // limpeza final de um desbloqueio foi interrompida. O conteúdo em claro
                // já está completo, então o cofre pode ser tratado como aberto.
                bool staleMetadataWithFiles = hasMetadata && files.Length > 0;
                if (!hasMetadata || staleMetadataWithFiles)
                {
                    return new VaultOverview
                    {
                        State = files.Length == 0 ? VaultState.Empty : VaultState.Unlocked,
                        FileCount = files.Length,
                        TotalBytes = totalBytes,
                        WorkingFolderPath = WorkingFolderPath,
                        LockedFolderPath = LockedFolderPath,
                        MetadataPath = MetadataPath
                    };
                }
            }

            if (!hasUnlockedFolder && hasLockedFolder && hasMetadata)
            {
                VaultMetadata metadata;
                try
                {
                    metadata = LoadMetadata();
                }
                catch
                {
                    return new VaultOverview
                    {
                        State = VaultState.Inconsistent,
                        WorkingFolderPath = WorkingFolderPath,
                        LockedFolderPath = LockedFolderPath,
                        MetadataPath = MetadataPath
                    };
                }

                DateTimeOffset? lockedAtUtc = DateTimeOffset.TryParse(metadata.LockedUtc, out DateTimeOffset parsedLockedAt)
                    ? parsedLockedAt
                    : null;

                return new VaultOverview
                {
                    State = VaultState.Locked,
                    FileCount = metadata.FileCount,
                    TotalBytes = metadata.TotalBytes,
                    WorkingFolderPath = WorkingFolderPath,
                    LockedFolderPath = LockedFolderPath,
                    MetadataPath = MetadataPath,
                    LockedAtUtc = lockedAtUtc
                };
            }

            return new VaultOverview
            {
                State = VaultState.Inconsistent,
                WorkingFolderPath = WorkingFolderPath,
                LockedFolderPath = LockedFolderPath,
                MetadataPath = MetadataPath
            };
        }

        public Task<LockResult> LockVaultAsync(string password, IProgress<VaultProgress>? progress, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => LockVault(password, progress, cancellationToken), cancellationToken);
        }

        public Task<UnlockResult> UnlockVaultWithPasswordAsync(string password, IProgress<VaultProgress>? progress, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => UnlockVault(password, false, progress, cancellationToken), cancellationToken);
        }

        public Task<UnlockResult> UnlockVaultWithRecoveryKeyAsync(string recoveryKey, IProgress<VaultProgress>? progress, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => UnlockVault(recoveryKey, true, progress, cancellationToken), cancellationToken);
        }

        public string[] GetWorkingFiles()
        {
            return Directory.Exists(WorkingFolderPath)
                ? EnumerateVaultFiles(WorkingFolderPath)
                : Array.Empty<string>();
        }

        private LockResult LockVault(string password, IProgress<VaultProgress>? progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Informe uma senha.");
            }

            VaultOverview overview = GetOverview();
            if (overview.State == VaultState.Locked)
            {
                throw new InvalidOperationException("O cofre já está bloqueado.");
            }

            if (overview.State == VaultState.Inconsistent)
            {
                throw new InvalidOperationException("O cofre está em um estado inconsistente. Verifique as pastas private/private.locked antes de continuar.");
            }

            string[] files = EnumerateVaultFiles(WorkingFolderPath);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("A pasta private está vazia. Adicione arquivos antes de bloquear o cofre.");
            }

            string tempLockedDirectory = LockedFolderPath + ".pending";
            string tempMetadataPath = MetadataPath + ".pending";
            string plaintextBackupDirectory = CreateUniqueBackupPath(WorkingFolderPath + ".plain.backup");

            CleanupDirectory(tempLockedDirectory);
            CleanupFile(tempMetadataPath);

            Directory.CreateDirectory(tempLockedDirectory);
            VaultSecurity.TryHardenPath(tempLockedDirectory);

            byte[] masterKey = VaultCrypto.CreateMasterKey();
            byte[] passwordSalt = Array.Empty<byte>();
            byte[] recoverySalt = Array.Empty<byte>();
            string recoveryKey = VaultCrypto.GenerateRecoveryKey();
            long totalBytes = 0;
            string? backupWarning = null;
            bool movedPlaintextToBackup = false;
            bool movedLockedIntoPlace = false;
            bool movedMetadataIntoPlace = false;

            try
            {
                for (int index = 0; index < files.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string file = files[index];
                    string relativePath = Path.GetRelativePath(WorkingFolderPath, file);
                    EnsureFileIsInsideDirectory(WorkingFolderPath, file, relativePath);
                    string normalizedPath = VaultCrypto.NormalizeRelativePath(relativePath);
                    string encryptedRelativePath = relativePath + LockedExtension;
                    string encryptedPath = CreateSafeDestinationPath(tempLockedDirectory, encryptedRelativePath);
                    string? encryptedDirectory = Path.GetDirectoryName(encryptedPath);

                    if (!string.IsNullOrWhiteSpace(encryptedDirectory))
                    {
                        Directory.CreateDirectory(encryptedDirectory);
                    }

                    long fileSize = new FileInfo(file).Length;
                    totalBytes += fileSize;
                    VaultCrypto.EncryptFileToPath(file, encryptedPath, masterKey, normalizedPath);

                    progress?.Report(new VaultProgress
                    {
                        Step = $"Criptografando {Path.GetFileName(relativePath)}",
                        Current = index + 1,
                        Total = files.Length
                    });
                }

                passwordSalt = VaultCrypto.CreateRandomBytes(VaultCrypto.SaltSize);
                recoverySalt = VaultCrypto.CreateRandomBytes(VaultCrypto.SaltSize);

                var metadata = new VaultMetadata
                {
                    Version = VaultMetadata.CurrentVersion,
                    WorkingFolderName = Path.GetFileName(WorkingFolderPath),
                    LockedFolderName = Path.GetFileName(LockedFolderPath),
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    LockedUtc = DateTime.UtcNow.ToString("O"),
                    FileCount = files.Length,
                    TotalBytes = totalBytes,
                    PasswordSaltBase64 = Convert.ToBase64String(passwordSalt),
                    WrappedMasterKeyByPassword = VaultCrypto.ProtectMasterKey(masterKey, password, passwordSalt, VaultCrypto.DefaultIterations),
                    RecoverySaltBase64 = Convert.ToBase64String(recoverySalt),
                    WrappedMasterKeyByRecovery = VaultCrypto.ProtectMasterKey(masterKey, VaultCrypto.NormalizeRecoveryKey(recoveryKey), recoverySalt, VaultCrypto.DefaultIterations)
                };

                WriteMetadataFile(tempMetadataPath, metadata);
                VaultSecurity.TryHardenPath(tempMetadataPath);

                Directory.Move(WorkingFolderPath, plaintextBackupDirectory);
                movedPlaintextToBackup = true;
                Directory.Move(tempLockedDirectory, LockedFolderPath);
                movedLockedIntoPlace = true;
                File.Move(tempMetadataPath, MetadataPath, overwrite: true);
                movedMetadataIntoPlace = true;
                VaultSecurity.TryHardenPath(LockedFolderPath);
                VaultSecurity.TryHardenPath(MetadataPath);
                TryApplyHiddenPresentation();

                try
                {
                    DeletePlaintextDirectory(plaintextBackupDirectory);
                }
                catch (Exception ex)
                {
                    backupWarning = $"Os arquivos foram bloqueados, mas a cópia temporária em '{plaintextBackupDirectory}' não foi removida automaticamente. Ela ainda contém dados em claro: {ex.Message}";
                }

                progress?.Report(new VaultProgress
                {
                    Step = "Cofre bloqueado com sucesso",
                    Current = files.Length,
                    Total = files.Length
                });

                return new LockResult
                {
                    FileCount = files.Length,
                    TotalBytes = totalBytes,
                    RecoveryKey = recoveryKey,
                    BackupWarning = backupWarning
                };
            }
            catch
            {
                if (movedLockedIntoPlace
                    && !movedMetadataIntoPlace
                    && Directory.Exists(LockedFolderPath)
                    && !Directory.Exists(tempLockedDirectory))
                {
                    try
                    {
                        Directory.Move(LockedFolderPath, tempLockedDirectory);
                    }
                    catch
                    {
                    }
                }

                TryCleanupDirectory(tempLockedDirectory);
                TryCleanupFile(tempMetadataPath);

                if (movedPlaintextToBackup
                    && Directory.Exists(plaintextBackupDirectory)
                    && !Directory.Exists(WorkingFolderPath)
                    && !movedMetadataIntoPlace)
                {
                    Directory.Move(plaintextBackupDirectory, WorkingFolderPath);
                }

                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
                CryptographicOperations.ZeroMemory(passwordSalt);
                CryptographicOperations.ZeroMemory(recoverySalt);
            }
        }

        private UnlockResult UnlockVault(string secret, bool isRecoveryKey, IProgress<VaultProgress>? progress, CancellationToken cancellationToken)
        {
            VaultOverview overview = GetOverview();
            if (overview.State != VaultState.Locked)
            {
                throw new InvalidOperationException("Não existe cofre bloqueado para restaurar.");
            }

            if (Directory.Exists(WorkingFolderPath))
            {
                throw new InvalidOperationException("A pasta private já existe. Mova ou remova a pasta atual antes de desbloquear.");
            }

            VaultMetadata metadata = LoadMetadata();
            byte[] passwordSalt = Array.Empty<byte>();
            byte[] recoverySalt = Array.Empty<byte>();
            byte[] masterKey = isRecoveryKey
                ? UnprotectWithRecoveryKey(metadata, secret, out recoverySalt)
                : UnprotectWithPassword(metadata, secret, out passwordSalt);

            CryptographicOperations.ZeroMemory(passwordSalt);
            CryptographicOperations.ZeroMemory(recoverySalt);

            string[] lockedFiles = EnumerateVaultFiles(LockedFolderPath);
            string[] unexpectedFiles = lockedFiles
                .Where(file => !file.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (unexpectedFiles.Length > 0)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new VaultIntegrityException("O cofre bloqueado contém arquivos inesperados. Remova itens não criptografados de private.locked antes de continuar.");
            }

            string[] encryptedFiles = lockedFiles
                .Where(file => file.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (encryptedFiles.Length == 0)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new VaultIntegrityException("O cofre bloqueado não contém arquivos criptografados.");
            }

            if (encryptedFiles.Length != metadata.FileCount)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new VaultIntegrityException("A quantidade de arquivos criptografados não bate com os metadados do cofre.");
            }

            string tempUnlockedDirectory = WorkingFolderPath + ".pending";
            string lockedBackupDirectory = CreateUniqueBackupPath(LockedFolderPath + ".encrypted.backup");
            DeletePlaintextDirectory(tempUnlockedDirectory);

            string? backupWarning = null;
            int restoredFileCount = 0;
            long restoredBytes = 0;

            try
            {
                RemoveHiddenPresentation();
                Directory.CreateDirectory(tempUnlockedDirectory);
                VaultSecurity.TryHardenPath(tempUnlockedDirectory);

                for (int index = 0; index < encryptedFiles.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string encryptedFile = encryptedFiles[index];
                    string encryptedRelativePath = Path.GetRelativePath(LockedFolderPath, encryptedFile);
                    string originalRelativePath = RemoveLockedExtension(encryptedRelativePath);
                    string normalizedPath = VaultCrypto.NormalizeRelativePath(originalRelativePath);
                    string outputPath = CreateSafeDestinationPath(tempUnlockedDirectory, originalRelativePath);
                    string? outputDirectory = Path.GetDirectoryName(outputPath);

                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    VaultCrypto.DecryptFileToPath(encryptedFile, outputPath, masterKey, normalizedPath);
                    restoredFileCount++;
                    restoredBytes += new FileInfo(outputPath).Length;

                    progress?.Report(new VaultProgress
                    {
                        Step = $"Restaurando {Path.GetFileName(originalRelativePath)}",
                        Current = index + 1,
                        Total = encryptedFiles.Length
                    });
                }

                if (restoredFileCount != metadata.FileCount || restoredBytes != metadata.TotalBytes)
                {
                    throw new VaultIntegrityException("O cofre restaurado não bate com os metadados. Os dados criptografados podem estar incompletos.");
                }

                Directory.Move(LockedFolderPath, lockedBackupDirectory);
                Directory.Move(tempUnlockedDirectory, WorkingFolderPath);
                VaultSecurity.TryHardenPath(WorkingFolderPath);

                try
                {
                    File.Delete(MetadataPath);
                }
                catch (Exception ex)
                {
                    backupWarning = CombineWarnings(
                        backupWarning,
                        $"O cofre foi restaurado, mas o arquivo de metadados em '{MetadataPath}' não foi removido automaticamente: {ex.Message}. Ele será substituído no próximo bloqueio.");
                }

                try
                {
                    Directory.Delete(lockedBackupDirectory, true);
                }
                catch (Exception ex)
                {
                    backupWarning = CombineWarnings(
                        backupWarning,
                        $"O cofre foi restaurado, mas os dados criptografados temporários em '{lockedBackupDirectory}' não foram removidos automaticamente: {ex.Message}");
                }

                progress?.Report(new VaultProgress
                {
                    Step = "Cofre desbloqueado com sucesso",
                    Current = encryptedFiles.Length,
                    Total = encryptedFiles.Length
                });

                return new UnlockResult
                {
                    FileCount = restoredFileCount,
                    TotalBytes = restoredBytes,
                    BackupWarning = backupWarning
                };
            }
            catch
            {
                TryDeletePlaintextDirectory(tempUnlockedDirectory);

                if (Directory.Exists(lockedBackupDirectory)
                    && !Directory.Exists(LockedFolderPath)
                    && !Directory.Exists(WorkingFolderPath))
                {
                    Directory.Move(lockedBackupDirectory, LockedFolderPath);
                }

                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
        }

        private VaultMetadata LoadMetadata()
        {
            VaultMetadata? metadata;
            try
            {
                string json = File.ReadAllText(MetadataPath, Utf8NoBom);
                metadata = JsonSerializer.Deserialize<VaultMetadata>(json);
            }
            catch (JsonException ex)
            {
                throw new VaultIntegrityException("Não foi possível ler os metadados do cofre.", ex);
            }
            catch (NotSupportedException ex)
            {
                throw new VaultIntegrityException("Não foi possível ler os metadados do cofre.", ex);
            }

            if (metadata == null)
            {
                throw new VaultIntegrityException("Não foi possível ler os metadados do cofre.");
            }

            ValidateMetadata(metadata);
            return metadata;
        }

        private static void WriteMetadataFile(string metadataPath, VaultMetadata metadata)
        {
            string metadataJson = JsonSerializer.Serialize(metadata, MetadataJsonOptions);
            byte[] metadataBytes = Utf8NoBom.GetBytes(metadataJson);
            try
            {
                using var output = new FileStream(
                    metadataPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    VaultCrypto.StreamBufferSize,
                    FileOptions.WriteThrough);
                output.Write(metadataBytes);
                output.Flush(true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(metadataBytes);
            }
        }

        private static byte[] UnprotectWithPassword(VaultMetadata metadata, string password, out byte[] passwordSalt)
        {
            passwordSalt = DecodeBase64Field(metadata.PasswordSaltBase64, "salt da senha", VaultCrypto.SaltSize);
            bool allowLegacyPasswordFallback = metadata.Version == VaultMetadata.LegacyPasswordNormalizationVersion;
            return VaultCrypto.UnprotectMasterKey(
                metadata.WrappedMasterKeyByPassword,
                password,
                passwordSalt,
                metadata.PasswordIterations,
                allowLegacyPasswordFallback);
        }

        private static byte[] UnprotectWithRecoveryKey(VaultMetadata metadata, string recoveryKey, out byte[] recoverySalt)
        {
            recoverySalt = DecodeBase64Field(metadata.RecoverySaltBase64, "salt da chave de recuperação", VaultCrypto.SaltSize);
            return VaultCrypto.UnprotectMasterKey(
                metadata.WrappedMasterKeyByRecovery,
                VaultCrypto.NormalizeRecoveryKey(recoveryKey),
                recoverySalt,
                metadata.RecoveryIterations);
        }

        private static void ValidateMetadata(VaultMetadata metadata)
        {
            if (metadata.Version != VaultMetadata.CurrentVersion
                && metadata.Version != VaultMetadata.PreviousVersion
                && metadata.Version != VaultMetadata.LegacyPasswordNormalizationVersion)
            {
                throw new VaultIntegrityException("Versão de metadados do cofre não suportada.");
            }

            if (metadata.WorkingFolderName != "private" || metadata.LockedFolderName != "private.locked")
            {
                throw new VaultIntegrityException("Os metadados do cofre apontam para pastas inesperadas.");
            }

            if (metadata.FileCount <= 0 || metadata.TotalBytes < 0)
            {
                throw new VaultIntegrityException("Os metadados do cofre contêm contagem ou tamanho inválido.");
            }

            if (!DateTimeOffset.TryParse(metadata.CreatedUtc, out _)
                || !DateTimeOffset.TryParse(metadata.LockedUtc, out _))
            {
                throw new VaultIntegrityException("Os metadados do cofre contêm datas inválidas.");
            }

            ValidateKdfMetadata(metadata.PasswordIterations, metadata.PasswordSaltBase64, metadata.WrappedMasterKeyByPassword, "senha");
            ValidateKdfMetadata(metadata.RecoveryIterations, metadata.RecoverySaltBase64, metadata.WrappedMasterKeyByRecovery, "chave de recuperação");
        }

        private static void ValidateKdfMetadata(int iterations, string saltBase64, string wrappedMasterKey, string label)
        {
            if (iterations < VaultCrypto.MinimumIterations || iterations > VaultCrypto.MaximumIterations)
            {
                throw new VaultIntegrityException($"Os metadados do cofre contêm iterações inválidas para {label}.");
            }

            byte[] salt = DecodeBase64Field(saltBase64, $"salt da {label}", VaultCrypto.SaltSize);
            byte[] wrappedKey = DecodeBase64Field(wrappedMasterKey, $"chave mestra protegida por {label}", VaultCrypto.WrappedMasterKeyPayloadSize);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(wrappedKey);
        }

        private static byte[] DecodeBase64Field(string? value, string fieldName, int expectedLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new VaultIntegrityException($"Os metadados do cofre não contêm {fieldName}.");
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(value);
            }
            catch (FormatException ex)
            {
                throw new VaultIntegrityException($"Os metadados do cofre contêm {fieldName} inválido.", ex);
            }

            if (decoded.Length != expectedLength)
            {
                CryptographicOperations.ZeroMemory(decoded);
                throw new VaultIntegrityException($"Os metadados do cofre contêm {fieldName} com tamanho inválido.");
            }

            return decoded;
        }

        private static string RemoveLockedExtension(string encryptedRelativePath)
        {
            if (!encryptedRelativePath.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new VaultIntegrityException($"Arquivo criptografado inesperado: {encryptedRelativePath}");
            }

            return encryptedRelativePath[..^LockedExtension.Length];
        }

        private static string CreateUniqueBackupPath(string basePath)
        {
            if (!Directory.Exists(basePath) && !File.Exists(basePath))
            {
                return basePath;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            for (int attempt = 1; attempt <= 100; attempt++)
            {
                string candidate = $"{basePath}.{timestamp}.{attempt:00}";
                if (!Directory.Exists(candidate) && !File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Não foi possível criar um caminho de backup temporário único.");
        }

        private static void CleanupDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void CleanupFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void TryCleanupDirectory(string path)
        {
            try
            {
                CleanupDirectory(path);
            }
            catch
            {
            }
        }

        private static void TryCleanupFile(string path)
        {
            try
            {
                CleanupFile(path);
            }
            catch
            {
            }
        }

        private static string CombineWarnings(string? current, string next)
        {
            return string.IsNullOrWhiteSpace(current)
                ? next
                : current + Environment.NewLine + next;
        }

        private static void DeletePlaintextDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string file in EnumerateVaultFiles(path))
            {
                WipeFileContents(file);
            }

            foreach (string directory in EnumerateVaultDirectories(path))
            {
                ClearDeletionBlockingAttributes(directory);
            }

            ClearDeletionBlockingAttributes(path);
            Directory.Delete(path, true);
        }

        private static void TryDeletePlaintextDirectory(string path)
        {
            try
            {
                DeletePlaintextDirectory(path);
            }
            catch
            {
            }
        }

        private static void WipeFileContents(string path)
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return;
            }

            ClearDeletionBlockingAttributes(path);
            if (fileInfo.Length <= 0)
            {
                return;
            }

            byte[] buffer = new byte[WipeBufferSize];
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None,
                    WipeBufferSize,
                    FileOptions.SequentialScan | FileOptions.WriteThrough);

                long remaining = fileInfo.Length;
                while (remaining > 0)
                {
                    int toWrite = (int)Math.Min(buffer.Length, remaining);
                    stream.Write(buffer, 0, toWrite);
                    remaining -= toWrite;
                }

                stream.SetLength(0);
                stream.Flush(true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }

        private static void ClearDeletionBlockingAttributes(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            attributes &= ~FileAttributes.ReadOnly;
            attributes &= ~FileAttributes.Hidden;
            attributes &= ~FileAttributes.System;
            File.SetAttributes(path, attributes);
        }

        private static string[] EnumerateVaultFiles(string rootPath)
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

        private static long GetTotalBytesSafe(IEnumerable<string> files)
        {
            long totalBytes = 0;

            foreach (string file in files)
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return totalBytes;
        }

        private static string[] EnumerateVaultDirectories(string rootPath)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            return Directory.EnumerateDirectories(rootPath, "*", options)
                .OrderByDescending(path => path.Length)
                .ToArray();
        }

        private static void EnsureFileIsInsideDirectory(string rootDirectory, string filePath, string relativePath)
        {
            string expectedPath = CreateSafeDestinationPath(rootDirectory, relativePath);
            string actualPath = Path.GetFullPath(filePath);

            if (!string.Equals(expectedPath, actualPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new VaultIntegrityException("A pasta private contém um caminho de arquivo inseguro.");
            }
        }

        private static string CreateSafeDestinationPath(string rootDirectory, string relativePath)
        {
            string fullRoot = Path.GetFullPath(rootDirectory);
            string fullDestination = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            string rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullDestination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new VaultIntegrityException("O cofre contém um caminho de arquivo inseguro.");
            }

            return fullDestination;
        }

        private void TryApplyHiddenPresentation()
        {
            try
            {
                ApplyHiddenPresentation();
            }
            catch
            {
            }
        }

        private void TryHardenKnownVaultPaths()
        {
            VaultSecurity.TryHardenPath(WorkingFolderPath);
            VaultSecurity.TryHardenPath(LockedFolderPath);
            VaultSecurity.TryHardenPath(MetadataPath);
        }

        private void ApplyHiddenPresentation()
        {
            if (Directory.Exists(LockedFolderPath))
            {
                FileAttributes directoryAttributes = File.GetAttributes(LockedFolderPath);
                directoryAttributes |= FileAttributes.Hidden;
                directoryAttributes |= FileAttributes.System;
                File.SetAttributes(LockedFolderPath, directoryAttributes);
            }

            if (File.Exists(MetadataPath))
            {
                FileAttributes fileAttributes = File.GetAttributes(MetadataPath);
                fileAttributes |= FileAttributes.Hidden;
                fileAttributes |= FileAttributes.System;
                File.SetAttributes(MetadataPath, fileAttributes);
            }
        }

        private void RemoveHiddenPresentation()
        {
            if (File.Exists(MetadataPath))
            {
                FileAttributes fileAttributes = File.GetAttributes(MetadataPath);
                fileAttributes &= ~FileAttributes.Hidden;
                fileAttributes &= ~FileAttributes.System;
                File.SetAttributes(MetadataPath, fileAttributes);
            }

            if (Directory.Exists(LockedFolderPath))
            {
                FileAttributes directoryAttributes = File.GetAttributes(LockedFolderPath);
                directoryAttributes &= ~FileAttributes.Hidden;
                directoryAttributes &= ~FileAttributes.System;
                File.SetAttributes(LockedFolderPath, directoryAttributes);
            }
        }
    }
}
