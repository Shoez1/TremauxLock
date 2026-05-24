using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TremauxLock
{
    internal static class VaultCrypto
    {
        private static readonly byte[] FileMagic = Encoding.ASCII.GetBytes("TMX2");
        private static readonly byte[] StreamFileMagic = Encoding.ASCII.GetBytes("TMX3");
        private static readonly byte[] WrapMagic = Encoding.ASCII.GetBytes("WRP2");
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int MasterKeySize = 32;
        private const int ChunkHeaderSize = 1 + sizeof(int) + NonceSize + TagSize;
        private const int MaxSupportedChunkSize = 64 * 1024 * 1024;

        public const int DefaultIterations = 210000;
        public const int MinimumIterations = 210000;
        public const int MaximumIterations = 2000000;
        public const int MinimumPasswordLength = 10;
        public const int SaltSize = 16;
        public const int WrappedMasterKeyPayloadSize = NonceSize + TagSize + MasterKeySize;
        public const int StreamChunkSize = 4 * 1024 * 1024;
        public const int StreamBufferSize = 1024 * 1024;

        public static byte[] CreateRandomBytes(int length) => RandomNumberGenerator.GetBytes(length);

        public static byte[] CreateMasterKey() => CreateRandomBytes(MasterKeySize);

        public static void EncryptFileToPath(string inputPath, string outputPath, byte[] masterKey, string relativePath)
        {
            try
            {
                using var input = new FileStream(
                    inputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StreamBufferSize,
                    FileOptions.SequentialScan);

                using var output = new FileStream(
                    outputPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    StreamBufferSize,
                    FileOptions.SequentialScan);

                EncryptStream(input, output, masterKey, relativePath);
            }
            catch
            {
                TryDeleteFile(outputPath);
                throw;
            }
        }

        public static void DecryptFileToPath(string inputPath, string outputPath, byte[] masterKey, string relativePath)
        {
            try
            {
                using var input = new FileStream(
                    inputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    StreamBufferSize,
                    FileOptions.SequentialScan);

                Span<byte> magic = stackalloc byte[FileMagic.Length];
                ReadExactlyOrThrow(input, magic, "O arquivo criptografado esta truncado ou invalido.");
                input.Position = 0;

                if (magic.SequenceEqual(StreamFileMagic))
                {
                    using var output = new FileStream(
                        outputPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        StreamBufferSize,
                        FileOptions.SequentialScan);

                    DecryptChunkedStream(input, output, masterKey, relativePath);
                    return;
                }

                if (magic.SequenceEqual(FileMagic))
                {
                    byte[] encryptedBytes = File.ReadAllBytes(inputPath);
                    byte[] plainBytes = DecryptFileBytes(encryptedBytes, masterKey, relativePath);
                    try
                    {
                        File.WriteAllBytes(outputPath, plainBytes);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(plainBytes);
                    }

                    return;
                }

                throw new VaultIntegrityException("Formato de arquivo criptografado desconhecido.");
            }
            catch
            {
                TryDeleteFile(outputPath);
                throw;
            }
        }

        private static void EncryptStream(Stream input, Stream output, byte[] masterKey, string relativePath)
        {
            byte[] plainBuffer = new byte[StreamChunkSize];
            byte[] cipherBuffer = new byte[StreamChunkSize];
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] chunkHeader = new byte[ChunkHeaderSize];
            string normalizedPath = NormalizeRelativePath(relativePath);

            try
            {
                output.Write(StreamFileMagic);
                Span<byte> chunkSizeBytes = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(chunkSizeBytes, StreamChunkSize);
                output.Write(chunkSizeBytes);

                using var aes = new AesGcm(masterKey, TagSize);
                long chunkIndex = 0;
                bool wroteFinalChunk = false;

                while (!wroteFinalChunk)
                {
                    int plainLength = ReadUpTo(input, plainBuffer);
                    bool isFinal = input.CanSeek
                        ? input.Position >= input.Length
                        : plainLength < plainBuffer.Length;

                    byte flags = isFinal ? (byte)1 : (byte)0;
                    RandomNumberGenerator.Fill(nonce);

                    byte[] aad = CreateChunkAad(normalizedPath, chunkIndex, flags, plainLength);
                    aes.Encrypt(
                        nonce,
                        plainBuffer.AsSpan(0, plainLength),
                        cipherBuffer.AsSpan(0, plainLength),
                        tag,
                        aad);

                    chunkHeader[0] = flags;
                    BinaryPrimitives.WriteInt32LittleEndian(chunkHeader.AsSpan(1, sizeof(int)), plainLength);
                    nonce.CopyTo(chunkHeader.AsSpan(1 + sizeof(int), NonceSize));
                    tag.CopyTo(chunkHeader.AsSpan(1 + sizeof(int) + NonceSize, TagSize));

                    output.Write(chunkHeader);
                    output.Write(cipherBuffer.AsSpan(0, plainLength));

                    wroteFinalChunk = isFinal;
                    chunkIndex++;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBuffer);
                CryptographicOperations.ZeroMemory(cipherBuffer);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(tag);
                CryptographicOperations.ZeroMemory(chunkHeader);
            }
        }

        private static void DecryptChunkedStream(Stream input, Stream output, byte[] masterKey, string relativePath)
        {
            Span<byte> fileHeader = stackalloc byte[StreamFileMagic.Length];
            Span<byte> chunkSizeBytes = stackalloc byte[sizeof(int)];

            ReadExactlyOrThrow(input, fileHeader, "O arquivo criptografado esta truncado ou invalido.");
            if (!fileHeader.SequenceEqual(StreamFileMagic))
            {
                throw new VaultIntegrityException("Formato de arquivo criptografado desconhecido.");
            }

            ReadExactlyOrThrow(input, chunkSizeBytes, "O arquivo criptografado esta truncado ou invalido.");
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(chunkSizeBytes);
            if (chunkSize <= 0 || chunkSize > MaxSupportedChunkSize)
            {
                throw new VaultIntegrityException("O arquivo criptografado usa um tamanho de bloco invalido.");
            }

            byte[] cipherBuffer = new byte[chunkSize];
            byte[] plainBuffer = new byte[chunkSize];
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
            string normalizedPath = NormalizeRelativePath(relativePath);

            try
            {
                using var aes = new AesGcm(masterKey, TagSize);
                long chunkIndex = 0;
                bool sawFinalChunk = false;

                while (!sawFinalChunk)
                {
                    int flagValue = input.ReadByte();
                    if (flagValue < 0)
                    {
                        throw new VaultIntegrityException("O arquivo criptografado terminou sem bloco final.");
                    }

                    byte flags = (byte)flagValue;
                    if ((flags & ~1) != 0)
                    {
                        throw new VaultIntegrityException("O arquivo criptografado contem flags invalidas.");
                    }

                    ReadExactlyOrThrow(input, lengthBytes, "O arquivo criptografado esta truncado ou invalido.");
                    int plainLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                    if (plainLength < 0 || plainLength > chunkSize)
                    {
                        throw new VaultIntegrityException("O arquivo criptografado contem bloco com tamanho invalido.");
                    }

                    ReadExactlyOrThrow(input, nonce, "O arquivo criptografado esta truncado ou invalido.");
                    ReadExactlyOrThrow(input, tag, "O arquivo criptografado esta truncado ou invalido.");
                    ReadExactlyOrThrow(input, cipherBuffer.AsSpan(0, plainLength), "O arquivo criptografado esta truncado ou invalido.");

                    byte[] aad = CreateChunkAad(normalizedPath, chunkIndex, flags, plainLength);
                    try
                    {
                        aes.Decrypt(
                            nonce,
                            cipherBuffer.AsSpan(0, plainLength),
                            tag,
                            plainBuffer.AsSpan(0, plainLength),
                            aad);
                    }
                    catch (CryptographicException ex)
                    {
                        CryptographicOperations.ZeroMemory(plainBuffer);
                        throw new VaultIntegrityException("Falha ao validar os dados do cofre. O arquivo pode ter sido alterado.", ex);
                    }

                    output.Write(plainBuffer.AsSpan(0, plainLength));
                    CryptographicOperations.ZeroMemory(plainBuffer.AsSpan(0, plainLength));
                    CryptographicOperations.ZeroMemory(cipherBuffer.AsSpan(0, plainLength));

                    sawFinalChunk = (flags & 1) == 1;
                    if (sawFinalChunk && input.Position != input.Length)
                    {
                        throw new VaultIntegrityException("O arquivo criptografado contem dados extras apos o bloco final.");
                    }

                    chunkIndex++;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cipherBuffer);
                CryptographicOperations.ZeroMemory(plainBuffer);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(tag);
            }
        }

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
            finally
            {
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(tag);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(aad);
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

        public static byte[] UnprotectMasterKey(
            string wrappedMasterKey,
            string secret,
            byte[] salt,
            int iterations,
            bool allowLegacyPasswordNormalizationFallback = false)
        {
            byte[] payload = DecodeWrappedMasterKeyPayload(wrappedMasterKey);
            try
            {
                try
                {
                    return UnprotectMasterKeyPayload(payload, secret, salt, iterations);
                }
                catch (VaultAuthenticationException)
                    when (allowLegacyPasswordNormalizationFallback && ShouldTryLegacyPasswordNormalization(secret))
                {
                    return UnprotectMasterKeyPayload(payload, NormalizeRecoveryKey(secret), salt, iterations);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }

        private static byte[] UnprotectMasterKeyPayload(byte[] payload, string secret, byte[] salt, int iterations)
        {
            byte[] wrappingKey = DeriveKey(secret, salt, iterations);

            if (payload.Length != WrappedMasterKeyPayloadSize)
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
            var normalized = new StringBuilder(recoveryKey.Length);
            foreach (char character in recoveryKey)
            {
                if (character == '-' || char.IsWhiteSpace(character))
                {
                    continue;
                }

                normalized.Append(char.ToUpperInvariant(character));
            }

            return normalized.ToString();
        }

        public static bool IsValidRecoveryKeyFormat(string recoveryKey)
        {
            if (string.IsNullOrWhiteSpace(recoveryKey))
            {
                return false;
            }

            string normalized = NormalizeRecoveryKey(recoveryKey);

            // Recovery keys are 32 hex characters (16 bytes)
            if (normalized.Length != 32)
            {
                return false;
            }

            // Verify all characters are valid hex
            foreach (char c in normalized)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        private static int ReadUpTo(Stream input, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = input.Read(buffer, totalRead, buffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        private static void ReadExactlyOrThrow(Stream input, Span<byte> buffer, string message)
        {
            try
            {
                input.ReadExactly(buffer);
            }
            catch (EndOfStreamException ex)
            {
                throw new VaultIntegrityException(message, ex);
            }
        }

        private static byte[] CreateChunkAad(string normalizedPath, long chunkIndex, byte flags, int plainLength)
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(normalizedPath);
            byte[] aad = new byte[StreamFileMagic.Length + sizeof(long) + 1 + sizeof(int) + pathBytes.Length];
            int offset = 0;

            Buffer.BlockCopy(StreamFileMagic, 0, aad, offset, StreamFileMagic.Length);
            offset += StreamFileMagic.Length;

            BinaryPrimitives.WriteInt64LittleEndian(aad.AsSpan(offset, sizeof(long)), chunkIndex);
            offset += sizeof(long);

            aad[offset] = flags;
            offset += 1;

            BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(offset, sizeof(int)), plainLength);
            offset += sizeof(int);

            Buffer.BlockCopy(pathBytes, 0, aad, offset, pathBytes.Length);
            return aad;
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
            if (secret == null)
            {
                throw new VaultAuthenticationException("A credencial informada esta incorreta ou os metadados do cofre foram alterados.");
            }

            if (salt.Length != SaltSize)
            {
                throw new VaultIntegrityException("Os metadados do cofre contem salt invalido.");
            }

            if (iterations < MinimumIterations || iterations > MaximumIterations)
            {
                throw new VaultIntegrityException("Os metadados do cofre contem parametro de derivacao invalido.");
            }

            return Rfc2898DeriveBytes.Pbkdf2(
                secret,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                MasterKeySize);
        }

        private static byte[] DecodeWrappedMasterKeyPayload(string wrappedMasterKey)
        {
            try
            {
                byte[] payload = Convert.FromBase64String(wrappedMasterKey);
                if (payload.Length != WrappedMasterKeyPayloadSize)
                {
                    CryptographicOperations.ZeroMemory(payload);
                    throw new VaultAuthenticationException("A credencial informada esta incorreta ou os metadados do cofre foram alterados.");
                }

                return payload;
            }
            catch (FormatException ex)
            {
                throw new VaultAuthenticationException("A credencial informada esta incorreta ou os metadados do cofre foram alterados.", ex);
            }
        }

        private static bool ShouldTryLegacyPasswordNormalization(string secret)
        {
            return !string.IsNullOrEmpty(secret)
                && secret.Contains("-", StringComparison.Ordinal)
                && NormalizeRecoveryKey(secret) != secret;
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
    }
}
