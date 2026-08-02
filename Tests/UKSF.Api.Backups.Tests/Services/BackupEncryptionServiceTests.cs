using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupEncryptionServiceTests
{
    private readonly BackupKeyPair _keyPair = BackupKeyGenerator.Generate();

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] Plaintext(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    private async Task<byte[]> Encrypt(byte[] plaintext, int frameSize = 1024, string publicKeyPem = null)
    {
        using var input = new MemoryStream(plaintext);
        using var output = new MemoryStream();
        await new BackupEncryptionService(frameSize).Encrypt(input, output, publicKeyPem ?? _keyPair.PublicKeyPem);
        return output.ToArray();
    }

    private async Task<byte[]> Decrypt(byte[] ciphertext, int frameSize = 1024, string privateKeyPem = null)
    {
        using var input = new MemoryStream(ciphertext);
        using var output = new MemoryStream();
        await new BackupEncryptionService(frameSize).Decrypt(input, output, privateKeyPem ?? _keyPair.PrivateKeyPem);
        return output.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(5000)]
    public async Task A_payload_round_trips_across_frame_boundaries(int length)
    {
        var plaintext = Plaintext(length);

        var result = await Decrypt(await Encrypt(plaintext));

        result.Should().Equal(plaintext);
    }

    [Fact]
    public async Task The_ciphertext_does_not_carry_the_plaintext()
    {
        var plaintext = Plaintext(4096);

        var ciphertext = await Encrypt(plaintext);

        ContainsSequence(ciphertext, plaintext.Take(64).ToArray()).Should().BeFalse();
        ciphertext.Length.Should().BeGreaterThan(plaintext.Length);
    }

    [Fact]
    public async Task The_file_is_tagged_as_UKSFBK1()
    {
        var ciphertext = await Encrypt(Plaintext(16));

        System.Text.Encoding.ASCII.GetString(ciphertext, 0, 7).Should().Be("UKSFBK1");
    }

    [Fact]
    public async Task Another_private_key_cannot_open_it()
    {
        var ciphertext = await Encrypt(Plaintext(2048));
        var other = BackupKeyGenerator.Generate();

        var act = () => Decrypt(ciphertext, privateKeyPem: other.PrivateKeyPem);

        (await act.Should().ThrowAsync<CryptographicException>()).Which.Message.Should().Contain("cannot be unwrapped");
    }

    [Fact]
    public async Task A_flipped_ciphertext_byte_fails_authentication()
    {
        var ciphertext = await Encrypt(Plaintext(2048));
        ciphertext[^1] ^= 0xFF;

        var act = () => Decrypt(ciphertext);

        (await act.Should().ThrowAsync<CryptographicException>()).Which.Message.Should().Contain("failed authentication");
    }

    [Fact]
    public async Task A_flipped_wrapped_key_byte_fails()
    {
        var ciphertext = await Encrypt(Plaintext(64));
        ciphertext[20] ^= 0xFF;

        var act = () => Decrypt(ciphertext);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task A_truncated_archive_is_rejected_rather_than_silently_short()
    {
        var plaintext = Plaintext(5000);
        var ciphertext = await Encrypt(plaintext);

        var act = () => Decrypt(ciphertext.Take(ciphertext.Length - 1500).ToArray());

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Dropping_whole_trailing_frames_is_rejected_because_the_final_frame_is_bound_in()
    {
        var plaintext = Plaintext(4096);
        var ciphertext = await Encrypt(plaintext);

        // Header plus exactly two whole frames, so the cut lands on a frame boundary.
        var frameLength = 4 + 12 + 16 + 1024;
        var header = 7 + 1 + 4 + 512;
        var act = () => Decrypt(ciphertext.Take(header + frameLength * 2).ToArray());

        (await act.Should().ThrowAsync<CryptographicException>()).Which.Message.Should().Contain("final frame");
    }

    [Fact]
    public async Task A_file_that_is_not_a_backup_archive_is_rejected()
    {
        var act = () => Decrypt("this is not a backup"u8.ToArray());

        (await act.Should().ThrowAsync<CryptographicException>()).Which.Message.Should().Contain("not a UKSFBK1 file");
    }

    [Fact]
    public async Task Reordered_frames_are_rejected()
    {
        var ciphertext = await Encrypt(Plaintext(3072));
        var frameLength = 4 + 12 + 16 + 1024;
        var header = 7 + 1 + 4 + 512;

        var first = ciphertext.Skip(header).Take(frameLength).ToArray();
        var second = ciphertext.Skip(header + frameLength).Take(frameLength).ToArray();
        var reordered = ciphertext.Take(header).Concat(second).Concat(first).Concat(ciphertext.Skip(header + frameLength * 2)).ToArray();

        var act = () => Decrypt(reordered);

        await act.Should().ThrowAsync<CryptographicException>();
    }
}
