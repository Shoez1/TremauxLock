using System;

namespace TremauxLock
{
    /// <summary>
    /// Metadados do modo "Ocultar rapido": só esconde a pasta de curiosos.
    /// Nao é criptografia — o conteudo continua em claro dentro de private.hidden.
    /// </summary>
    internal sealed class HideMetadata
    {
        public const string CurrentVersion = "hide1";

        public string Version { get; set; } = CurrentVersion;
        public string HiddenFolderName { get; set; } = "private.hidden";
        public string HiddenUtc { get; set; } = DateTime.UtcNow.ToString("O");
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public string PasswordSaltBase64 { get; set; } = string.Empty;
        public string PasswordHashBase64 { get; set; } = string.Empty;
    }
}
