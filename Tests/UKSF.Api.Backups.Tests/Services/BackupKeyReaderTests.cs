using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupKeyReaderTests
{
    private readonly BackupKeyPair _keyPair = BackupKeyGenerator.Generate();

    [Fact]
    public void A_pem_as_written_by_keygen_imports()
    {
        BackupKeyReader.ImportPublicKey(_keyPair.PublicKeyPem).KeySize.Should().Be(4096);
        BackupKeyReader.ImportPrivateKey(_keyPair.PrivateKeyPem).KeySize.Should().Be(4096);
    }

    [Fact]
    public void A_pem_pasted_into_a_single_line_field_imports()
    {
        var singleLine = _keyPair.PublicKeyPem.Replace("\r", string.Empty).Replace("\n", " ");

        BackupKeyReader.ImportPublicKey(singleLine).KeySize.Should().Be(4096);
    }

    [Fact]
    public void A_key_with_the_markers_stripped_off_imports()
    {
        var body = string.Concat(_keyPair.PublicKeyPem.Split('\n').Where(x => !x.StartsWith("-----")));

        BackupKeyReader.ImportPublicKey(body).KeySize.Should().Be(4096);
    }

    [Fact]
    public void Leading_and_trailing_whitespace_from_a_terminal_copy_imports()
    {
        BackupKeyReader.ImportPublicKey($"   \t{_keyPair.PublicKeyPem}\n\n  ").KeySize.Should().Be(4096);
    }

    [Fact]
    public void An_empty_key_is_rejected()
    {
        var act = () => BackupKeyReader.ImportPublicKey("   ");

        act.Should().Throw<CryptographicException>().Which.Message.Should().Contain("No key was provided");
    }

    [Fact]
    public void A_key_that_is_not_base64_is_rejected_with_advice()
    {
        var act = () => BackupKeyReader.ImportPublicKey("-----BEGIN PUBLIC KEY-----not a key-----END PUBLIC KEY-----");

        act.Should().Throw<CryptographicException>().Which.Message.Should().Contain("not valid base64");
    }

    [Fact]
    public void A_private_key_offered_as_a_public_key_is_rejected()
    {
        var act = () => BackupKeyReader.ImportPublicKey(_keyPair.PrivateKeyPem);

        act.Should().Throw<CryptographicException>();
    }
}
