using System.Security.Cryptography;

namespace UKSF.Api.Backups.Services;

/// <summary>
///     Keys arrive from the variables page, which is a single-line input, so a pasted PEM loses its line breaks.
///     Rebuild the key from its base64 body rather than rejecting a key that is perfectly valid.
/// </summary>
public static class BackupKeyReader
{
    private static readonly string[] Markers =
    [
        "-----BEGIN PUBLIC KEY-----",
        "-----END PUBLIC KEY-----",
        "-----BEGIN PRIVATE KEY-----",
        "-----END PRIVATE KEY-----"
    ];

    public static RSA ImportPublicKey(string key)
    {
        return Import(key, (rsa, bytes) => rsa.ImportSubjectPublicKeyInfo(bytes, out _));
    }

    public static RSA ImportPrivateKey(string key)
    {
        return Import(key, (rsa, bytes) => rsa.ImportPkcs8PrivateKey(bytes, out _));
    }

    private static RSA Import(string key, Action<RSA, byte[]> import)
    {
        var bytes = ToBytes(key);
        var rsa = RSA.Create();

        try
        {
            import(rsa, bytes);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }

        return rsa;
    }

    private static byte[] ToBytes(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new CryptographicException("No key was provided");
        }

        var body = Markers.Aggregate(key, (current, marker) => current.Replace(marker, string.Empty));
        body = new string(body.Where(x => !char.IsWhiteSpace(x)).ToArray());

        try
        {
            return Convert.FromBase64String(body);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Key is not valid base64 - copy the whole contents of the PEM file", exception);
        }
    }
}
