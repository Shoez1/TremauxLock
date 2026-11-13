using System.Security.Cryptography;

internal sealed class TempVault : IDisposable
{
    public TempVault()
    {
        Root = Path.Combine(Path.GetTempPath(), "TremauxLock.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Service = new VaultService(Root);
    }

    public string Root { get; }

    public VaultService Service { get; }

    public string WorkingFolder => Path.Combine(Root, "private");

    public string LockedFolder => Path.Combine(Root, "private.locked");

    public string MetadataPath => Path.Combine(Root, "private.vault.json");

    public void CreateWorkspace() => Service.EnsureWorkspace();

    public string WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(WorkingFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string WriteFile(string relativePath, byte[] content)
    {
        string path = Path.Combine(WorkingFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        try
        {
            ClearAttributes(Root);
            Directory.Delete(Root, true);
        }
        catch
        {
        }
    }

    private static void ClearAttributes(string root)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                attributes &= ~FileAttributes.ReadOnly;
                attributes &= ~FileAttributes.Hidden;
                attributes &= ~FileAttributes.System;
                File.SetAttributes(path, attributes);
            }
            catch
            {
            }
        }
    }
}
