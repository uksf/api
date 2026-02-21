using System.Net.Mail;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using Xunit;

namespace UKSF.Api.Core.Tests.Commands;

public class SendBasicEmailCommandTests
{
    private readonly Mock<ISmtpClientContext> _mockSmtpClientContext = new();
    private readonly SendBasicEmailCommand _subject;

    public SendBasicEmailCommandTests()
    {
        _subject = new SendBasicEmailCommand(_mockSmtpClientContext.Object);
    }

    [Fact]
    public async Task Should_send_html_email_with_correct_recipient_subject_and_body()
    {
        var args = new SendBasicEmailCommandArgs("recipient@test.com", "Test Subject", "<p>Hello</p>");

        MailMessage capturedMail = null;
        _mockSmtpClientContext.Setup(x => x.SendEmailAsync(It.IsAny<MailMessage>())).Callback<MailMessage>(m => capturedMail = m).Returns(Task.CompletedTask);

        await _subject.ExecuteAsync(args);

        capturedMail.Should().NotBeNull();
        capturedMail.To.ToString().Should().Contain("recipient@test.com");
        capturedMail.Subject.Should().Be("Test Subject");
        capturedMail.Body.Should().Be("<p>Hello</p>");
        capturedMail.IsBodyHtml.Should().BeTrue();
    }
}
