using System.Security.Cryptography;
using UKSF.Api.Backups.Services;

namespace UKSF.BackupRestore;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteUsage();
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "keygen"      => KeyGen(Arguments.Parse(args)),
                "decrypt"     => await Decrypt(Arguments.Parse(args)),
                "verify"      => await Verify(Arguments.Parse(args)),
                "drive-setup" => await DriveSetup.Run(Arguments.Parse(args)),
                _             => Unknown(args[0])
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed: {exception.Message}");
            return 1;
        }
    }

    private static int KeyGen(Arguments arguments)
    {
        var publicPath = arguments.Require("public");
        var privatePath = arguments.Require("private");

        var keyPair = BackupKeyGenerator.Generate();
        File.WriteAllText(publicPath, keyPair.PublicKeyPem);
        File.WriteAllText(privatePath, keyPair.PrivateKeyPem);

        Console.WriteLine($"Public key  : {publicPath}   (goes in the API variable BACKUP_PUBLIC_KEY)");
        Console.WriteLine($"Private key : {privatePath}   (store offline - without it every backup is unreadable)");
        return 0;
    }

    private static async Task<int> Decrypt(Arguments arguments)
    {
        var inputPath = arguments.Require("input");
        var outputPath = arguments.Require("output");
        var keyPath = arguments.Require("key");

        await using var input = File.OpenRead(inputPath);
        await using var output = File.Create(outputPath);
        await new BackupEncryptionService().Decrypt(input, output, await File.ReadAllTextAsync(keyPath));

        Console.WriteLine($"Wrote {outputPath}");
        return 0;
    }

    /// <summary>Proves a key pair can protect and recover real data, before it is trusted with the only copy of anything.</summary>
    private static async Task<int> Verify(Arguments arguments)
    {
        var publicKeyPem = await File.ReadAllTextAsync(arguments.Require("public"));
        var privateKeyPem = await File.ReadAllTextAsync(arguments.Require("private"));

        var plaintext = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);
        var service = new BackupEncryptionService();

        using var encrypted = new MemoryStream();
        await service.Encrypt(new MemoryStream(plaintext), encrypted, publicKeyPem);

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await service.Decrypt(encrypted, decrypted, privateKeyPem);

        var matches = decrypted.ToArray().SequenceEqual(plaintext);
        Console.WriteLine($"Plaintext : {plaintext.Length} bytes, sha256 {Convert.ToHexString(SHA256.HashData(plaintext))}");
        Console.WriteLine($"Recovered : {decrypted.Length} bytes, sha256 {Convert.ToHexString(SHA256.HashData(decrypted.ToArray()))}");
        Console.WriteLine(matches ? "Key pair verified" : "KEY PAIR FAILED - recovered data does not match");
        return matches ? 0 : 1;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("uksf-backup-restore keygen --public <path> --private <path>");
        Console.WriteLine("uksf-backup-restore decrypt --input <archive.zip.enc> --output <archive.zip> --key <private.pem>");
        Console.WriteLine("uksf-backup-restore verify  --public <public.pem> --private <private.pem>");
        Console.WriteLine("uksf-backup-restore drive-setup --client-id <id> --client-secret <secret> [--folder <name>]");
    }
}
