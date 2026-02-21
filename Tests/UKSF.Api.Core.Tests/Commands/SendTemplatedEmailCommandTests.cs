using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Queries;
using Xunit;

namespace UKSF.Api.Core.Tests.Commands;

public class SendTemplatedEmailCommandTests
{
    private readonly Mock<IGetEmailTemplateQuery> _mockGetEmailTemplateQuery = new();
    private readonly Mock<ISmtpClientContext> _mockSmtpClientContext = new();
    private readonly SendTemplatedEmailCommand _subject;

    public SendTemplatedEmailCommandTests()
    {
        _subject = new SendTemplatedEmailCommand(_mockGetEmailTemplateQuery.Object, _mockSmtpClientContext.Object);
    }

    [Fact]
    public async Task Should_resolve_template_and_send_html_email()
    {
        var substitutions = new Dictionary<string, string> { { "name", "John" } };
        var args = new SendTemplatedEmailCommandArgs("recipient@test.com", "Welcome Email", "welcome", substitutions);

        _mockGetEmailTemplateQuery
            .Setup(x => x.ExecuteAsync(It.Is<GetEmailTemplateQueryArgs>(a => a.TemplateName == "welcome" && a.Substitutions == substitutions)))
            .ReturnsAsync("<html>Hello John</html>");

        MailMessage capturedMail = null;
        _mockSmtpClientContext.Setup(x => x.SendEmailAsync(It.IsAny<MailMessage>())).Callback<MailMessage>(m => capturedMail = m).Returns(Task.CompletedTask);

        await _subject.ExecuteAsync(args);

        _mockGetEmailTemplateQuery.Verify(
            x => x.ExecuteAsync(It.Is<GetEmailTemplateQueryArgs>(a => a.TemplateName == "welcome" && a.Substitutions == substitutions)),
            Times.Once
        );
        capturedMail.Should().NotBeNull();
        capturedMail.To.ToString().Should().Contain("recipient@test.com");
        capturedMail.Subject.Should().Be("Welcome Email");
        capturedMail.Body.Should().Be("<html>Hello John</html>");
        capturedMail.IsBodyHtml.Should().BeTrue();
    }
}
