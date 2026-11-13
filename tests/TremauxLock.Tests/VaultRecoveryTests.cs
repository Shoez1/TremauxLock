using System.Security.Cryptography;
using System.Text.Json;

public sealed class VaultRecoveryTests
{
    private const string ValidPassword = "SenhaForte!2026";

    [Fact]
    public void EnsureWorkspace_RestoresPlaintextBackupWhenNoVaultFoldersExist()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        string backup = Path.Combine(temp.Root, "private.plain.backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "resgatado.txt"), "conteudo resgatado");
        Directory.Delete(temp.WorkingFolder, true);

        temp.Service.EnsureWorkspace();

        Assert.True(File.Exists(Path.Combine(temp.WorkingFolder, "resgatado.txt")));
        Assert.Equal("conteudo resgatado", File.ReadAllText(Path.Combine(temp.WorkingFolder, "resgatado.txt")));
        Assert.False(Directory.Exists(backup));
        Assert.False(string.IsNullOrWhiteSpace(temp.Service.RecoveryNotice));
    }

    [Fact]
    public void EnsureWorkspace_RollsBackInterruptedLockWhenEncryptedFolderHasNoMetadata()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        string backup = Path.Combine(temp.Root, "private.plain.backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "resgatado.txt"), "conteudo");
        Directory.Delete(temp.WorkingFolder, true);
        Directory.CreateDirectory(temp.LockedFolder);
        File.WriteAllBytes(Path.Combine(temp.LockedFolder, "resgatado.txt.tmx"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(temp.Root, "private.vault.json.pending"), "{}");

        temp.Service.EnsureWorkspace();

        Assert.True(File.Exists(Path.Combine(temp.WorkingFolder, "resgatado.txt")));
        Assert.False(Directory.Exists(temp.LockedFolder));
        Assert.False(File.Exists(Path.Combine(temp.Root, "private.vault.json.pending")));
        Assert.False(Directory.Exists(backup));
        Assert.Contains("interromp", temp.Service.RecoveryNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureWorkspace_CompletesInterruptedUnlockWhenPendingPlaintextExists()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("nota.txt", "conteudo recuperado");
        await temp.Service.LockVaultAsync(ValidPassword, null);

        VaultMetadata metadata = ReadMetadata(temp);
        byte[] salt = Convert.FromBase64String(metadata.PasswordSaltBase64);
        byte[] masterKey = VaultCrypto.UnprotectMasterKey(
            metadata.WrappedMasterKeyByPassword,
            ValidPassword,
            salt,
            metadata.PasswordIterations);

        string pending = temp.WorkingFolder + ".pending";
        Directory.CreateDirectory(pending);
        try
        {
            VaultCrypto.DecryptFileToPath(
                Directory.GetFiles(temp.LockedFolder, "*.tmx", SearchOption.AllDirectories).Single(),
                Path.Combine(pending, "nota.txt"),
                masterKey,
                "nota.txt");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(salt);
        }

        string encryptedBackup = temp.LockedFolder + ".encrypted.backup";
        Directory.Move(temp.LockedFolder, encryptedBackup);

        temp.Service.EnsureWorkspace();

        Assert.True(File.Exists(Path.Combine(temp.WorkingFolder, "nota.txt")));
        Assert.Equal("conteudo recuperado", File.ReadAllText(Path.Combine(temp.WorkingFolder, "nota.txt")));
        Assert.False(Directory.Exists(pending));
        Assert.False(Directory.Exists(encryptedBackup));
        Assert.False(File.Exists(temp.MetadataPath));
        Assert.Contains("desbloque", temp.Service.RecoveryNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureWorkspace_CleansPlaintextResidueWhenVaultIsLocked()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("nota.txt", "conteudo");
        await temp.Service.LockVaultAsync(ValidPassword, null);

        string pending = temp.WorkingFolder + ".pending";
        Directory.CreateDirectory(pending);
        File.WriteAllText(Path.Combine(pending, "vazado.txt"), "plaintext");
        string backup = Path.Combine(temp.Root, "private.plain.backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "vazado.txt"), "plaintext");

        temp.Service.EnsureWorkspace();

        Assert.False(Directory.Exists(pending));
        Assert.False(Directory.Exists(backup));
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.True(File.Exists(temp.MetadataPath));
        Assert.Equal(VaultState.Locked, temp.Service.GetOverview().State);
        Assert.False(string.IsNullOrWhiteSpace(temp.Service.RecoveryNotice));
    }

    [Fact]
    public async Task GetOverview_TreatsPrivateFolderWithStaleMetadataAsUnlocked()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("nota.txt", "conteudo");
        await temp.Service.LockVaultAsync(ValidPassword, null);

        string encryptedBackup = temp.LockedFolder + ".encrypted.backup";
        Directory.Move(temp.LockedFolder, encryptedBackup);
        Directory.CreateDirectory(temp.WorkingFolder);
        File.WriteAllText(Path.Combine(temp.WorkingFolder, "nota.txt"), "conteudo");

        VaultOverview overview = temp.Service.GetOverview();

        Assert.Equal(VaultState.Unlocked, overview.State);
        Assert.Equal(1, overview.FileCount);
    }

    [Fact]
    public async Task LockVault_WithStaleMetadata_OverwritesMetadata()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("nota.txt", "conteudo");
        await temp.Service.LockVaultAsync(ValidPassword, null);

        string encryptedBackup = temp.LockedFolder + ".encrypted.backup";
        Directory.Move(temp.LockedFolder, encryptedBackup);
        Directory.CreateDirectory(temp.WorkingFolder);
        File.WriteAllText(Path.Combine(temp.WorkingFolder, "nota.txt"), "conteudo atualizado");

        LockResult result = await temp.Service.LockVaultAsync(ValidPassword, null);

        Assert.Equal(1, result.FileCount);
        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.True(File.Exists(temp.MetadataPath));
        Assert.Equal(VaultState.Locked, temp.Service.GetOverview().State);
    }

    private static VaultMetadata ReadMetadata(TempVault temp)
    {
        string json = File.ReadAllText(temp.MetadataPath);
        return JsonSerializer.Deserialize<VaultMetadata>(json)
            ?? throw new InvalidOperationException("Metadados nao puderam ser lidos.");
    }
}
