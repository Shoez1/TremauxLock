using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TremauxLock
{
    internal static class VaultCrypto
    {
        private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("TMX2");
        private static readonly byte[] WrapMagic = Encoding.ASCII.GetBytes("WRP2");
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int MasterKeySize = 32;

        public const int DefaultIterations = 210000;
        public const int MinimumPasswordLength = 10;

        public static byte[] CreateRandomBytes(int length) => RandomNumberGenerator.GetBytes(length);

        public static byte[] CreateMasterKey() => CreateRandomBytes(MasterKeySize);

        public static byte[] EncryptFileBytes(byte[] plainBytes, byte[] masterKey, string relativePath)
        {
            byte[] nonce = CreateRandomBytes(NonceSize);
            byte[] ciphertext = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSize];
            byte[] aad = Encoding.UTF8.GetBytes(NormalizeRelativePath(relativePath));

            using (var aes = new AesGcm(masterKey, TagSize))
            {
                aes.Encrypt(nonce, plainBytes, ciphertext, tag, aad);
            }

            byte[] output = new byte[FileMagic.Length + nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(FileMagic, 0, output, 0, FileMagic.Length);
            Buffer.BlockCopy(nonce, 0, output, FileMagic.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, output, FileMagic.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, output, FileMagic.Length + nonce.Length + tag.Length, ciphertext.Length);
            return output;
        }

        public static byte[] DecryptFileBytes(byte[] encryptedBytes, byte[] masterKey, string relativePath)
        {
            if (encryptedBytes.Length < FileMagic.Length + NonceSize + TagSize)
            {
                throw new VaultIntegrityException("O arquivo criptografado esta truncado ou invalido.");
            }

            for (int index = 0; index < FileMagic.Length; index++)
            {
                if (encryptedBytes[index] != FileMagic[index])
                {
                    throw new VaultIntegrityException("Formato de arquivo criptografado desconhecido.");
                }
            }

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[encryptedBytes.Length - FileMagic.Length - NonceSize - TagSize];
            byte[] plaintext = new byte[ciphertext.Length];
            byte[] aad = Encoding.UTF8.GetBytes(NormalizeRelativePath(relativePath));

            Buffer.BlockCopy(encryptedBytes, FileMagic.Length, nonce, 0, nonce.Length);
            Buffer.BlockCopy(encryptedBytes, FileMagic.Length + nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(encryptedBytes, FileMagic.Length + nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

            try
            {
                using var aes = new AesGcm(masterKey, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new VaultIntegrityException("Falha ao validar os dados do cofre. O arquivo pode ter sido alterado.", ex);
            }
        }

        public static string ProtectMasterKey(byte[] masterKey, string secret, byte[] salt, int iterations)
        {
            byte[] wrappingKey = DeriveKey(secret, salt, iterations);
            byte[] nonce = CreateRandomBytes(NonceSize);
            byte[] ciphertext = new byte[masterKey.Length];
            byte[] tag = new byte[TagSize];

            try
            {
                using var aes = new AesGcm(wrappingKey, TagSize);
                aes.Encrypt(nonce, masterKey, ciphertext, tag, WrapMagic);

                byte[] payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
                Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
                Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
                Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
                return Convert.ToBase64String(payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(wrappingKey);
            }
        }

        public static byte[] UnprotectMasterKey(string wrappedMasterKey, string secret, byte[] salt, int iterations)
        {
            byte[] wrappingKey = DeriveKey(secret, salt, iterations);
            byte[] payload = Convert.FromBase64String(wrappedMasterKey);

            if (payload.Length < NonceSize + TagSize + MasterKeySize)
            {
                CryptographicOperations.ZeroMemory(wrappingKey);
                throw new VaultAuthenticationException("A credencial informada esta incorreta ou os metadados do cofre foram alterados.");
            }

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[payload.Length - NonceSize - TagSize];
            byte[] masterKey = new byte[ciphertext.Length];

            Buffer.BlockCopy(payload, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(payload, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(payload, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

            try
            {
                using var aes = new AesGcm(wrappingKey, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, masterKey, WrapMagic);
                return masterKey;
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(masterKey);
                throw new VaultAuthenticationException("A credencial informada esta incorreta ou os metadados do cofre foram alterados.", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(wrappingKey);
            }
        }

        public static string GenerateRecoveryKey()
        {
            byte[] bytes = CreateRandomBytes(16);
            string hex = Convert.ToHexString(bytes);
            var groups = new List<string>();
            for (int index = 0; index < hex.Length; index += 4)
            {
                groups.Add(hex.Substring(index, Math.Min(4, hex.Length - index)));
            }

            return string.Join("-", groups);
        }

        public static string NormalizeRecoveryKey(string recoveryKey)
        {
            return recoveryKey
                .Trim()
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
        }

        public static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }

        public static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int suffixIndex = 0;

            while (value >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                value /= 1024;
                suffixIndex++;
            }

            return suffixIndex == 0
                ? $"{value:0} {suffixes[suffixIndex]}"
                : $"{value:0.0} {suffixes[suffixIndex]}";
        }

        private static byte[] DeriveKey(string secret, byte[] salt, int iterations)
        {
            string normalizedSecret = secret.Contains("-", StringComparison.Ordinal)
                ? NormalizeRecoveryKey(secret)
                : secret;

            return Rfc2898DeriveBytes.Pbkdf2(
                normalizedSecret,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                MasterKeySize);
        }
    }
}
