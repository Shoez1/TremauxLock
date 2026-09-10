using System;

namespace TremauxLock
{
    internal sealed class VaultMetadata
    {
        public const string CurrentVersion = "3.1";
        public const string LegacyPasswordNormalizationVersion = "3.0";
        public const int MaxPasswordHintLength = 140;

        public string Version { get; set; } = CurrentVersion;
        public string WorkingFolderName { get; set; } = "private";
        public string LockedFolderName { get; set; } = "private.locked";
        public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
        public string LockedUtc { get; set; } = DateTime.UtcNow.ToString("O");
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public int PasswordIterations { get; set; } = VaultCrypto.DefaultIterations;
        public string PasswordSaltBase64 { get; set; } = string.Empty;
        public string WrappedMasterKeyByPassword { get; set; } = string.Empty;
        public int RecoveryIterations { get; set; } = VaultCrypto.DefaultIterations;
        public string RecoverySaltBase64 { get; set; } = string.Empty;
        public string WrappedMasterKeyByRecovery { get; set; } = string.Empty;
        /// <summary>
        /// Dica de senha opcional, em claro. Nunca deve conter a propria senha.
        /// </summary>
        public string PasswordHint { get; set; } = string.Empty;
    }
}
