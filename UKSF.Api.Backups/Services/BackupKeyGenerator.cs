using System.Security.Cryptography;

namespace UKSF.Api.Backups.Services;

public record BackupKeyPair(string PublicKeyPem, string PrivateKeyPem);

public static class BackupKeyGenerator
{
    private const int KeySizeBits = 4096;

    public static BackupKeyPair Generate()
    {
        using var rsa = RSA.Create(KeySizeBits);
        return new BackupKeyPair(rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }
}
