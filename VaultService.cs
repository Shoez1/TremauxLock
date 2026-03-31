using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public void EnsureWorkspace()
        {
            if (!Directory.Exists(WorkingFolderPath) && !Directory.Exists(LockedFolderPath))
            {
                Directory.CreateDirectory(WorkingFolderPath);
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
                hasUnlockedFolder = true;
            }

            if (hasUnlockedFolder && !hasLockedFolder && !hasMetadata)
            {
                string[] files = Directory.GetFiles(WorkingFolderPath, "*", SearchOption.AllDirectories);
                long totalBytes = files.Sum(file => new FileInfo(file).Length);

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

            if (!hasUnlockedFolder && hasLockedFolder && hasMetadata)
            {
                VaultMetadata metadata = LoadMetadata();
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

        private LockResult LockVault(string password, IProgress<VaultProgress>? progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < VaultCrypto.MinimumPasswordLength)
            {
                throw new InvalidOperationException($"Use uma senha com pelo menos {VaultCrypto.MinimumPasswordLength} caracteres.");
            }

            VaultOverview overview = GetOverview();
            if (overview.State == VaultState.Locked)
            {
                throw new InvalidOperationException("O cofre ja esta bloqueado.");
            }

            if (overview.State == VaultState.Inconsistent)
            {
                throw new InvalidOperationException("O cofre esta em um estado inconsistente. Verifique as pastas private/private.locked antes de continuar.");
            }

            string[] files = Directory.GetFiles(WorkingFolderPath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("A pasta private esta vazia. Adicione arquivos antes de bloquear o cofre.");
            }

            string tempLockedDirectory = LockedFolderPath + ".pending";
            string tempMetadataPath = MetadataPath + ".pending";
            string plaintextBackupDirectory = WorkingFolderPath + ".plain.backup";

            CleanupDirectory(tempLockedDirectory);
            CleanupDirectory(plaintextBackupDirectory);
            CleanupFile(tempMetadataPath);

            Directory.CreateDirectory(tempLockedDirectory);

            byte[] masterKey = VaultCrypto.CreateMasterKey();
            string recoveryKey = VaultCrypto.GenerateRecoveryKey();
            long totalBytes = 0;
            string? backupWarning = null;

            try
            {
                for (int index = 0; index < files.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string file = files[index];
                    string relativePath = Path.GetRelativePath(WorkingFolderPath, file);
                    string normalizedPath = VaultCrypto.NormalizeRelativePath(relativePath);
                    string encryptedRelativePath = relativePath + LockedExtension;
                    string encryptedPath = Path.Combine(tempLockedDirectory, encryptedRelativePath);
                    string? encryptedDirectory = Path.GetDirectoryName(encryptedPath);

                    if (!string.IsNullOrWhiteSpace(encryptedDirectory))
                    {
                        Directory.CreateDirectory(encryptedDirectory);
                    }

                    byte[] plainBytes = File.ReadAllBytes(file);
                    totalBytes += plainBytes.LongLength;

                    try
                    {
                        byte[] encryptedBytes = VaultCrypto.EncryptFileBytes(plainBytes, masterKey, normalizedPath);
                        File.WriteAllBytes(encryptedPath, encryptedBytes);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(plainBytes);
                    }

                    progress?.Report(new VaultProgress
                    {
                        Step = $"Criptografando {Path.GetFileName(relativePath)}",
                        Current = index + 1,
                        Total = files.Length
                    });
                }

                byte[] passwordSalt = VaultCrypto.CreateRandomBytes(16);
                byte[] recoverySalt = VaultCrypto.CreateRandomBytes(16);

                var metadata = new VaultMetadata
                {
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

                string metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tempMetadataPath, metadataJson);

                Directory.Move(WorkingFolderPath, plaintextBackupDirectory);
                Directory.Move(tempLockedDirectory, LockedFolderPath);
                File.Move(tempMetadataPath, MetadataPath);
                ApplyHiddenPresentation();

                try
                {
                    Directory.Delete(plaintextBackupDirectory, true);
                }
                catch (Exception ex)
                {
                    backupWarning = $"Os arquivos foram bloqueados, mas a copia temporaria em '{plaintextBackupDirectory}' nao foi removida automaticamente: {ex.Message}";
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
                CleanupDirectory(tempLockedDirectory);
                CleanupFile(tempMetadataPath);

                if (Directory.Exists(plaintextBackupDirectory)
                    && !Directory.Exists(WorkingFolderPath)
                    && !Directory.Exists(LockedFolderPath))
                {
                    Directory.Move(plaintextBackupDirectory, WorkingFolderPath);
                }

                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
        }

        private UnlockResult UnlockVault(string secret, bool isRecoveryKey, IProgress<VaultProgress>? progress, CancellationToken cancellationToken)
        {
            VaultOverview overview = GetOverview();
            if (overview.State != VaultState.Locked)
            {
                throw new InvalidOperationException("Nao existe cofre bloqueado para restaurar.");
            }

            if (Directory.Exists(WorkingFolderPath))
            {
                throw new InvalidOperationException("A pasta private ja existe. Mova ou remova a pasta atual antes de desbloquear.");
            }

            VaultMetadata metadata = LoadMetadata();
            byte[] masterKey = isRecoveryKey
                ? VaultCrypto.UnprotectMasterKey(
                    metadata.WrappedMasterKeyByRecovery,
                    VaultCrypto.NormalizeRecoveryKey(secret),
                    Convert.FromBase64String(metadata.RecoverySaltBase64),
                    metadata.RecoveryIterations)
                : VaultCrypto.UnprotectMasterKey(
                    metadata.WrappedMasterKeyByPassword,
                    secret,
                    Convert.FromBase64String(metadata.PasswordSaltBase64),
                    metadata.PasswordIterations);

            string[] encryptedFiles = Directory.GetFiles(LockedFolderPath, "*" + LockedExtension, SearchOption.AllDirectories);
            if (encryptedFiles.Length == 0)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new VaultIntegrityException("O cofre bloqueado nao contem arquivos criptografados.");
            }

            string tempUnlockedDirectory = WorkingFolderPath + ".pending";
            string lockedBackupDirectory = LockedFolderPath + ".encrypted.backup";
            CleanupDirectory(tempUnlockedDirectory);
            CleanupDirectory(lockedBackupDirectory);

            string? backupWarning = null;

            try
            {
                RemoveHiddenPresentation();
                Directory.CreateDirectory(tempUnlockedDirectory);

                for (int index = 0; index < encryptedFiles.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string encryptedFile = encryptedFiles[index];
                    string encryptedRelativePath = Path.GetRelativePath(LockedFolderPath, encryptedFile);
                    string originalRelativePath = RemoveLockedExtension(encryptedRelativePath);
                    string normalizedPath = VaultCrypto.NormalizeRelativePath(originalRelativePath);
                    string outputPath = Path.Combine(tempUnlockedDirectory, originalRelativePath);
                    string? outputDirectory = Path.GetDirectoryName(outputPath);

                    if (!string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    byte[] encryptedBytes = File.ReadAllBytes(encryptedFile);
                    byte[] plainBytes = VaultCrypto.DecryptFileBytes(encryptedBytes, masterKey, normalizedPath);

                    try
                    {
                        File.WriteAllBytes(outputPath, plainBytes);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(plainBytes);
                    }

                    progress?.Report(new VaultProgress
                    {
                        Step = $"Restaurando {Path.GetFileName(originalRelativePath)}",
                        Current = index + 1,
                        Total = encryptedFiles.Length
                    });
                }

                Directory.Move(LockedFolderPath, lockedBackupDirectory);
                Directory.Move(tempUnlockedDirectory, WorkingFolderPath);
                File.Delete(MetadataPath);

                try
                {
                    Directory.Delete(lockedBackupDirectory, true);
                }
                catch (Exception ex)
                {
                    backupWarning = $"O cofre foi restaurado, mas os dados criptografados temporarios em '{lockedBackupDirectory}' nao foram removidos automaticamente: {ex.Message}";
                }

                progress?.Report(new VaultProgress
                {
                    Step = "Cofre desbloqueado com sucesso",
                    Current = encryptedFiles.Length,
                    Total = encryptedFiles.Length
                });

                return new UnlockResult
                {
                    FileCount = metadata.FileCount,
                    TotalBytes = metadata.TotalBytes,
                    BackupWarning = backupWarning
                };
            }
            catch
            {
                CleanupDirectory(tempUnlockedDirectory);

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
            string json = File.ReadAllText(MetadataPath);
            VaultMetadata? metadata = JsonSerializer.Deserialize<VaultMetadata>(json);
            if (metadata == null)
            {
                throw new VaultIntegrityException("Nao foi possivel ler os metadados do cofre.");
            }

            return metadata;
        }

        private static string RemoveLockedExtension(string encryptedRelativePath)
        {
            if (!encryptedRelativePath.EndsWith(LockedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new VaultIntegrityException($"Arquivo criptografado inesperado: {encryptedRelativePath}");
            }

            return encryptedRelativePath[..^LockedExtension.Length];
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
