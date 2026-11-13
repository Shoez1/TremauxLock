using System.Security.Cryptography;
using System.Text;

public sealed class VaultCryptoTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "TremauxLock.Tests.Crypto",
        Guid.NewGuid().ToString("N"));

    public VaultCryptoTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch
        {
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(4 * 1024 * 1024)]
    [InlineData((4 * 1024 * 1024) + 123)]
    public void EncryptFileToPath_RoundTripsContent(int size)
    {
        byte[] content = RandomNumberGenerator.GetBytes(size);
        string input = Path.Combine(root, $"input-{size}.bin");
        string encrypted = Path.Combine(root, $"input-{size}.tmx");
        string decrypted = Path.Combine(root, $"input-{size}.out");
        File.WriteAllBytes(input, content);
        byte[] key = VaultCrypto.CreateMasterKey();

        VaultCrypto.EncryptFileToPath(input, encrypted, key, "sub\\arquivo.bin");
        VaultCrypto.DecryptFileToPath(encrypted, decrypted, key, "sub\\arquivo.bin");

        Assert.Equal(content, File.ReadAllBytes(decrypted));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void DecryptFileToPath_WrongRelativePath_FailsIntegrityCheck()
    {
        string input = Path.Combine(root, "path.txt");
        string encrypted = Path.Combine(root, "path.tmx");
        string decrypted = Path.Combine(root, "path.out");
        File.WriteAllText(input, "conteudo");
        byte[] key = VaultCrypto.CreateMasterKey();

        VaultCrypto.EncryptFileToPath(input, encrypted, key, "original.txt");

        Assert.Throws<VaultIntegrityException>(
            () => VaultCrypto.DecryptFileToPath(encrypted, decrypted, key, "outro.txt"));
        Assert.False(File.Exists(decrypted));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void DecryptFileToPath_TamperedCiphertext_FailsIntegrityCheck()
    {
        string encrypted = CreateEncryptedFile("tamper.txt", "conteudo confidencial", out byte[] key);
        byte[] bytes = File.ReadAllBytes(encrypted);
        bytes[^1] ^= 0x5A;
        File.WriteAllBytes(encrypted, bytes);
        string output = Path.Combine(root, "tamper.out");

        Assert.Throws<VaultIntegrityException>(
            () => VaultCrypto.DecryptFileToPath(encrypted, output, key, "tamper.txt"));
        Assert.False(File.Exists(output));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void DecryptFileToPath_TruncatedFile_FailsIntegrityCheck()
    {
        string encrypted = CreateEncryptedFile("truncated.txt", "conteudo confidencial", out byte[] key);
        byte[] bytes = File.ReadAllBytes(encrypted);
        File.WriteAllBytes(encrypted, bytes[..^3]);
        string output = Path.Combine(root, "truncated.out");

        Assert.Throws<VaultIntegrityException>(
            () => VaultCrypto.DecryptFileToPath(encrypted, output, key, "truncated.txt"));
        Assert.False(File.Exists(output));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void DecryptFileToPath_ExtraDataAfterFinalChunk_FailsIntegrityCheck()
    {
        string encrypted = CreateEncryptedFile("extra.txt", "conteudo confidencial", out byte[] key);
        File.AppendAllText(encrypted, "extra");
        string output = Path.Combine(root, "extra.out");

        Assert.Throws<VaultIntegrityException>(
            () => VaultCrypto.DecryptFileToPath(encrypted, output, key, "extra.txt"));
        Assert.False(File.Exists(output));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void LegacyTmx2Format_RoundTrips()
    {
        byte[] content = Encoding.UTF8.GetBytes("formato antigo TMX2");
        byte[] key = VaultCrypto.CreateMasterKey();
        byte[] encrypted = VaultCrypto.EncryptFileBytes(content, key, "legado.txt");
        string encryptedPath = Path.Combine(root, "legado.tmx");
        string output = Path.Combine(root, "legado.out");
        File.WriteAllBytes(encryptedPath, encrypted);

        VaultCrypto.DecryptFileToPath(encryptedPath, output, key, "legado.txt");

        Assert.Equal(content, File.ReadAllBytes(output));
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void ProtectMasterKey_RoundTripsWithCorrectSecret()
    {
        byte[] masterKey = VaultCrypto.CreateMasterKey();
        byte[] salt = VaultCrypto.CreateRandomBytes(VaultCrypto.SaltSize);
        string wrapped = VaultCrypto.ProtectMasterKey(masterKey, "SenhaForte!2026", salt, VaultCrypto.DefaultIterations);

        byte[] recovered = VaultCrypto.UnprotectMasterKey(wrapped, "SenhaForte!2026", salt, VaultCrypto.DefaultIterations);

        Assert.Equal(masterKey, recovered);
        CryptographicOperations.ZeroMemory(masterKey);
        CryptographicOperations.ZeroMemory(recovered);
    }

    [Fact]
    public void ProtectMasterKey_WrongSecretFailsAuthentication()
    {
        byte[] masterKey = VaultCrypto.CreateMasterKey();
        byte[] salt = VaultCrypto.CreateRandomBytes(VaultCrypto.SaltSize);
        string wrapped = VaultCrypto.ProtectMasterKey(masterKey, "SenhaForte!2026", salt, VaultCrypto.DefaultIterations);

        Assert.Throws<VaultAuthenticationException>(
            () => VaultCrypto.UnprotectMasterKey(wrapped, "SenhaErrada!2026", salt, VaultCrypto.DefaultIterations));
        CryptographicOperations.ZeroMemory(masterKey);
    }

    [Fact]
    public void LegacyPasswordNormalizationFallback_OnlyAppliesWhenRequested()
    {
        byte[] masterKey = VaultCrypto.CreateMasterKey();
        byte[] salt = VaultCrypto.CreateRandomBytes(VaultCrypto.SaltSize);
        const string legacySecret = "ABCD-EF01-2345-6789";
        string normalized = VaultCrypto.NormalizeRecoveryKey(legacySecret);
        string wrapped = VaultCrypto.ProtectMasterKey(masterKey, normalized, salt, VaultCrypto.DefaultIterations);

        Assert.Throws<VaultAuthenticationException>(
            () => VaultCrypto.UnprotectMasterKey(wrapped, legacySecret, salt, VaultCrypto.DefaultIterations));

        byte[] recovered = VaultCrypto.UnprotectMasterKey(
            wrapped,
            legacySecret,
            salt,
            VaultCrypto.DefaultIterations,
            allowLegacyPasswordNormalizationFallback: true);

        Assert.Equal(masterKey, recovered);
        CryptographicOperations.ZeroMemory(masterKey);
        CryptographicOperations.ZeroMemory(recovered);
    }

    [Fact]
    public void RecoveryKeyFormat_AcceptsGeneratedKeyAndRejectsInvalidInput()
    {
        string key = VaultCrypto.GenerateRecoveryKey();

        Assert.True(VaultCrypto.IsValidRecoveryKeyFormat(key));
        Assert.True(VaultCrypto.IsValidRecoveryKeyFormat(key.ToLowerInvariant()));
        Assert.False(VaultCrypto.IsValidRecoveryKeyFormat("ABCD-1234"));
        Assert.False(VaultCrypto.IsValidRecoveryKeyFormat("ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZ"));
        Assert.False(VaultCrypto.IsValidRecoveryKeyFormat(string.Empty));
    }

    private string CreateEncryptedFile(string name, string content, out byte[] key)
    {
        string input = Path.Combine(root, name);
        string encrypted = Path.Combine(root, name + ".tmx");
        File.WriteAllText(input, content);
        key = VaultCrypto.CreateMasterKey();
        VaultCrypto.EncryptFileToPath(input, encrypted, key, name);
        return encrypted;
    }
}
