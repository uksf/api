using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context;

public class SmtpClientContextVerifyModeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"uksf-smtp-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static IOptions<AppSettings> Settings(string username, string password)
    {
        return Options.Create(
            new AppSettings { Secrets = new AppSettings.SecretsConfig { Email = new AppSettings.SecretsConfig.EmailConfig { Username = username, Password = password } } }
        );
    }

    [Fact]
    public async Task SendEmailAsync_InVerifyMode_WritesTheMessageToTheEmailDirectory()
    {
        var subject = new SmtpClientContext(Settings("real@uksf.co.uk", "secret"), new VerifyMode("1", _directory));

        await subject.SendEmailAsync(new MailMessage { To = { "recruit@example.com" }, Subject = "Application received", Body = "Hello" });

        var files = Directory.GetFiles(_directory, "*.eml");
        files.Should().ContainSingle();
        var text = await File.ReadAllTextAsync(files.Single());
        text.Should().Contain("Subject: Application received");
        text.Should().Contain("To: recruit@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_InVerifyMode_WritesEvenWithoutCredentials()
    {
        var subject = new SmtpClientContext(Settings("", ""), new VerifyMode("1", _directory));

        await subject.SendEmailAsync(new MailMessage { To = { "recruit@example.com" }, Subject = "No creds", Body = "Hello" });

        Directory.GetFiles(_directory, "*.eml").Should().ContainSingle();
    }

    [Fact]
    public async Task SendEmailAsync_OutsideVerifyMode_WithoutCredentials_WritesNothing()
    {
        var subject = new SmtpClientContext(Settings("", ""), new VerifyMode(null, _directory));

        await subject.SendEmailAsync(new MailMessage { To = { "recruit@example.com" }, Subject = "Skipped", Body = "Hello" });

        Directory.Exists(_directory).Should().BeFalse();
    }
}
