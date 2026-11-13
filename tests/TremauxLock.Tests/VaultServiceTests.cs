using System.Security.Cryptography;
using System.Text.Json;

public sealed class VaultServiceTests
{
    private const string ValidPassword = "SenhaForte!2026";

    [Fact]
    public void EnsureWorkspace_CreatesWorkingFolder()
    {
        using var temp = new TempVault();

        temp.CreateWorkspace();

        Assert.True(Directory.Exists(temp.WorkingFolder));
        Assert.Equal(VaultState.Empty, temp.Service.GetOverview().State);
    }

    [Fact]
    public async Task LockAndUnlockWithPassword_RestoresFilesAndMetadata()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("nota.txt", "conteudo secreto");
        string nestedFile = temp.WriteFile(Path.Combine("sub", "dados.bin"), new string('x', 5000));
        string nestedHash = TempVault.HashFile(nestedFile);

        LockResult lockResult = await temp.Service.LockVaultAsync(ValidPassword, null);

        Assert.Equal(2, lockResult.FileCount);
        Assert.True(VaultCrypto.IsValidRecoveryKeyFormat(lockResult.RecoveryKey));
        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.True(File.Exists(temp.MetadataPath));
        Assert.Equal(2, Directory.GetFiles(temp.LockedFolder, "*.tmx", SearchOption.AllDirectories).Length);

        VaultMetadata metadata = ReadMetadata(temp);
        Assert.Equal(2, metadata.FileCount);
        Assert.Equal(VaultState.Locked, temp.Service.GetOverview().State);
        Assert.Equal(2, temp.Service.GetOverview().FileCount);

        UnlockResult unlockResult = await temp.Service.UnlockVaultWithPasswordAsync(ValidPassword, null);

        Assert.Equal(2, unlockResult.FileCount);
        Assert.Equal("conteudo secreto", File.ReadAllText(Path.Combine(temp.WorkingFolder, "nota.txt")));
        Assert.Equal(nestedHash, TempVault.HashFile(Path.Combine(temp.WorkingFolder, "sub", "dados.bin")));
        Assert.False(Directory.Exists(temp.LockedFolder));
        Assert.False(File.Exists(temp.MetadataPath));
        Assert.Equal(VaultState.Unlocked, temp.Service.GetOverview().State);
    }

    [Fact]
    public async Task NewVault_UsesCurrentMetadataVersionAndKdfBaseline()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("arquivo.txt", "conteudo");

        await temp.Service.LockVaultAsync(ValidPassword, null);

        VaultMetadata metadata = ReadMetadata(temp);
        Assert.Equal("3.2", metadata.Version);
        Assert.True(metadata.PasswordIterations >= 600_000);
        Assert.True(metadata.RecoveryIterations >= 600_000);
    }

    [Fact]
    public void LegacyVaultsRemainReadableThroughMinimumKdfIterations()
    {
        Assert.Equal(210_000, VaultCrypto.MinimumIterations);
        Assert.True(VaultCrypto.DefaultIterations > VaultCrypto.MinimumIterations);
    }

    [Fact]
    public async Task UnlockWithRecoveryKey_RestoresFiles()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("recuperavel.txt", "dados recuperaveis");
        LockResult lockResult = await temp.Service.LockVaultAsync(ValidPassword, null);

        UnlockResult unlockResult = await temp.Service.UnlockVaultWithRecoveryKeyAsync(lockResult.RecoveryKey, null);

        Assert.Equal(1, unlockResult.FileCount);
        Assert.Equal("dados recuperaveis", File.ReadAllText(Path.Combine(temp.WorkingFolder, "recuperavel.txt")));
    }

    [Fact]
    public async Task UnlockWithWrongPassword_KeepsVaultLocked()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("protegido.txt", "conteudo");
        await temp.Service.LockVaultAsync(ValidPassword, null);

        await Assert.ThrowsAsync<VaultAuthenticationException>(
            () => temp.Service.UnlockVaultWithPasswordAsync("SenhaErrada!2026", null));

        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.True(File.Exists(temp.MetadataPath));
        Assert.Equal(VaultState.Locked, temp.Service.GetOverview().State);
    }

    [Fact]
    public async Task UnlockWithTamperedFile_FailsWithoutLeavingPlaintextPending()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("integridade.txt", "conteudo confidencial");
        await temp.Service.LockVaultAsync(ValidPassword, null);
        string encryptedFile = Directory.GetFiles(temp.LockedFolder, "*.tmx", SearchOption.AllDirectories).Single();
        byte[] originalBytes = File.ReadAllBytes(encryptedFile);
        byte[] tamperedBytes = originalBytes.ToArray();
        tamperedBytes[^1] ^= 0x5A;
        File.WriteAllBytes(encryptedFile, tamperedBytes);

        await Assert.ThrowsAsync<VaultIntegrityException>(
            () => temp.Service.UnlockVaultWithPasswordAsync(ValidPassword, null));

        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.False(Directory.Exists(temp.WorkingFolder + ".pending"));
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.True(File.Exists(temp.MetadataPath));

        File.WriteAllBytes(encryptedFile, originalBytes);
        UnlockResult unlockResult = await temp.Service.UnlockVaultWithPasswordAsync(ValidPassword, null);
        Assert.Equal(1, unlockResult.FileCount);
        Assert.Equal("conteudo confidencial", File.ReadAllText(Path.Combine(temp.WorkingFolder, "integridade.txt")));
    }

    [Fact]
    public async Task UnlockWithUnexpectedFile_RefusesToProceed()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("original.txt", "conteudo");
        await temp.Service.LockVaultAsync(ValidPassword, null);
        File.WriteAllText(Path.Combine(temp.LockedFolder, "intruso.txt"), "nao criptografado");

        await Assert.ThrowsAsync<VaultIntegrityException>(
            () => temp.Service.UnlockVaultWithPasswordAsync(ValidPassword, null));

        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.True(Directory.Exists(temp.LockedFolder));
    }

    [Fact]
    public async Task UnlockWithMissingEncryptedFile_RefusesToProceed()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("a.txt", "a");
        temp.WriteFile("b.txt", "b");
        await temp.Service.LockVaultAsync(ValidPassword, null);
        string encryptedFile = Directory.GetFiles(temp.LockedFolder, "*.tmx", SearchOption.AllDirectories).First();
        File.Delete(encryptedFile);

        await Assert.ThrowsAsync<VaultIntegrityException>(
            () => temp.Service.UnlockVaultWithPasswordAsync(ValidPassword, null));

        Assert.False(Directory.Exists(temp.WorkingFolder));
        Assert.True(Directory.Exists(temp.LockedFolder));
    }

    [Fact]
    public async Task LockEmptyVault_IsRejected()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => temp.Service.LockVaultAsync(ValidPassword, null));

        Assert.True(Directory.Exists(temp.WorkingFolder));
        Assert.False(Directory.Exists(temp.LockedFolder));
    }

    [Fact]
    public async Task LockWithShortPassword_IsAccepted()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("arquivo.txt", "conteudo");

        LockResult lockResult = await temp.Service.LockVaultAsync("1234", null);

        Assert.Equal(1, lockResult.FileCount);
        Assert.True(Directory.Exists(temp.LockedFolder));
        Assert.False(Directory.Exists(temp.WorkingFolder));

        UnlockResult unlockResult = await temp.Service.UnlockVaultWithPasswordAsync("1234", null);

        Assert.Equal(1, unlockResult.FileCount);
        Assert.Equal("conteudo", File.ReadAllText(Path.Combine(temp.WorkingFolder, "arquivo.txt")));
    }

    [Fact]
    public void GetWorkingFiles_ReturnsNestedFilesInStableOrder()
    {
        using var temp = new TempVault();
        temp.CreateWorkspace();
        temp.WriteFile("z.txt", "z");
        temp.WriteFile(Path.Combine("a", "b.txt"), "b");

        string[] files = temp.Service.GetWorkingFiles();

        Assert.Equal(2, files.Length);
        Assert.EndsWith(Path.Combine("a", "b.txt"), files[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("z.txt", files[1], StringComparison.OrdinalIgnoreCase);
    }

    private static VaultMetadata ReadMetadata(TempVault temp)
    {
        string json = File.ReadAllText(temp.MetadataPath);
        return JsonSerializer.Deserialize<VaultMetadata>(json)
            ?? throw new InvalidOperationException("Metadados nao puderam ser lidos.");
    }
}
