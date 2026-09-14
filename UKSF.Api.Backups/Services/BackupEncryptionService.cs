using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace UKSF.Api.Backups.Services;

public interface IBackupEncryptionService
{
    Task Encrypt(Stream input, Stream output, string publicKeyPem, CancellationToken cancellationToken = default);
    Task Decrypt(Stream input, Stream output, string privateKeyPem, CancellationToken cancellationToken = default);
}

/// <summary>
///     Envelope format `UKSFBK1`: a per-archive AES-256 key wrapped with the RSA public key, then AES-GCM frames.
///     The dedi holds only the public key, so a stolen box or a stolen Drive account yields ciphertext.
/// </summary>
public class BackupEncryptionService : IBackupEncryptionService
{
    private const int DefaultFrameSize = 64 * 1024 * 1024;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const byte Version = 1;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("UKSFBK1");

    private readonly int _frameSize;

    public BackupEncryptionService() : this(DefaultFrameSize) { }

    internal BackupEncryptionService(int frameSize)
    {
        _frameSize = frameSize;
    }

    public async Task Encrypt(Stream input, Stream output, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        var key = RandomNumberGenerator.GetBytes(KeySize);

        using (var rsa = BackupKeyReader.ImportPublicKey(publicKeyPem))
        {
            var wrappedKey = rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
            await WriteHeader(output, wrappedKey, cancellationToken);
        }

        using var aes = new AesGcm(key, TagSize);
        var plaintext = new byte[_frameSize];
        var frameIndex = 0L;
        var pending = await input.ReadAtLeastAsync(plaintext, plaintext.Length, false, cancellationToken);

        do
        {
            var next = new byte[_frameSize];
            var following = await input.ReadAtLeastAsync(next, next.Length, false, cancellationToken);
            var isLast = following == 0;

            await WriteFrame(output, aes, plaintext.AsMemory(0, pending), frameIndex, isLast, cancellationToken);

            if (isLast)
            {
                return;
            }

            plaintext = next;
            pending = following;
            frameIndex++;
        }
        while (true);
    }

    public async Task Decrypt(Stream input, Stream output, string privateKeyPem, CancellationToken cancellationToken = default)
    {
        var wrappedKey = await ReadHeader(input, cancellationToken);

        using var rsa = BackupKeyReader.ImportPrivateKey(privateKeyPem);
        var key = Unwrap(rsa, wrappedKey);

        using var aes = new AesGcm(key, TagSize);
        var frameIndex = 0L;

        while (true)
        {
            var lengthBuffer = new byte[4];
            var read = await input.ReadAtLeastAsync(lengthBuffer, 4, false, cancellationToken);
            if (read == 0)
            {
                throw new CryptographicException("Backup archive ended before its final frame");
            }

            if (read < 4)
            {
                throw new CryptographicException("Backup archive is truncated");
            }

            var isLast = await ReadFrame(input, output, aes, BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer), frameIndex, cancellationToken);
            if (isLast)
            {
                return;
            }

            frameIndex++;
        }
    }

    private static byte[] Unwrap(RSA rsa, byte[] wrappedKey)
    {
        try
        {
            return rsa.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException("Backup archive cannot be unwrapped with this private key", exception);
        }
    }

    private static async Task WriteHeader(Stream output, byte[] wrappedKey, CancellationToken cancellationToken)
    {
        var header = new byte[Magic.Length + 1 + 4];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(Magic.Length + 1), wrappedKey.Length);

        await output.WriteAsync(header, cancellationToken);
        await output.WriteAsync(wrappedKey, cancellationToken);
    }

    private static async Task<byte[]> ReadHeader(Stream input, CancellationToken cancellationToken)
    {
        var header = new byte[Magic.Length + 1 + 4];
        if (await input.ReadAtLeastAsync(header, header.Length, false, cancellationToken) < header.Length)
        {
            throw new CryptographicException("Backup archive is not a UKSFBK1 file");
        }

        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic) || header[Magic.Length] != Version)
        {
            throw new CryptographicException("Backup archive is not a UKSFBK1 file");
        }

        var wrappedKeyLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(Magic.Length + 1));
        if (wrappedKeyLength is <= 0 or > 4096)
        {
            throw new CryptographicException("Backup archive header is malformed");
        }

        var wrappedKey = new byte[wrappedKeyLength];
        if (await input.ReadAtLeastAsync(wrappedKey, wrappedKeyLength, false, cancellationToken) < wrappedKeyLength)
        {
            throw new CryptographicException("Backup archive is truncated");
        }

        return wrappedKey;
    }

    private static async Task WriteFrame(
        Stream output,
        AesGcm aes,
        ReadOnlyMemory<byte> plaintext,
        long frameIndex,
        bool isLast,
        CancellationToken cancellationToken
    )
    {
        var nonce = Nonce(frameIndex);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        aes.Encrypt(nonce, plaintext.Span, ciphertext, tag, AssociatedData(frameIndex, isLast));

        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, ciphertext.Length);

        await output.WriteAsync(length, cancellationToken);
        await output.WriteAsync(nonce, cancellationToken);
        await output.WriteAsync(tag, cancellationToken);
        await output.WriteAsync(ciphertext, cancellationToken);
    }

    private static async Task<bool> ReadFrame(
        Stream input,
        Stream output,
        AesGcm aes,
        int ciphertextLength,
        long frameIndex,
        CancellationToken cancellationToken
    )
    {
        if (ciphertextLength < 0)
        {
            throw new CryptographicException("Backup archive frame is malformed");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[ciphertextLength];

        if (await input.ReadAtLeastAsync(nonce, NonceSize, false, cancellationToken) < NonceSize ||
            await input.ReadAtLeastAsync(tag, TagSize, false, cancellationToken) < TagSize ||
            await input.ReadAtLeastAsync(ciphertext, ciphertextLength, false, cancellationToken) < ciphertextLength)
        {
            throw new CryptographicException("Backup archive is truncated");
        }

        var plaintext = new byte[ciphertextLength];
        if (TryDecrypt(aes, nonce, ciphertext, tag, plaintext, frameIndex, true))
        {
            await output.WriteAsync(plaintext, cancellationToken);
            return true;
        }

        if (!TryDecrypt(aes, nonce, ciphertext, tag, plaintext, frameIndex, false))
        {
            throw new CryptographicException("Backup archive failed authentication - it is corrupt, reordered or tampered with");
        }

        await output.WriteAsync(plaintext, cancellationToken);
        return false;
    }

    private static bool TryDecrypt(AesGcm aes, byte[] nonce, byte[] ciphertext, byte[] tag, byte[] plaintext, long frameIndex, bool isLast)
    {
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, AssociatedData(frameIndex, isLast));
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Binds each frame to its position and to being the final frame, so reordering or truncation fails to authenticate.</summary>
    private static byte[] AssociatedData(long frameIndex, bool isLast)
    {
        var associatedData = new byte[Magic.Length + 1 + 8 + 1];
        Magic.CopyTo(associatedData, 0);
        associatedData[Magic.Length] = Version;
        BinaryPrimitives.WriteInt64LittleEndian(associatedData.AsSpan(Magic.Length + 1), frameIndex);
        associatedData[^1] = isLast ? (byte)1 : (byte)0;
        return associatedData;
    }

    private static byte[] Nonce(long frameIndex)
    {
        var nonce = new byte[NonceSize];
        BinaryPrimitives.WriteInt64LittleEndian(nonce.AsSpan(NonceSize - 8), frameIndex);
        return nonce;
    }
}
