using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Queries;
using Xunit;

namespace UKSF.Api.Core.Tests.Queries;

public class GetEmailTemplateQueryTests
{
    private readonly Mock<IFileContext> _mockFileContext = new();
    private readonly GetEmailTemplateQuery _subject;

    public GetEmailTemplateQueryTests()
    {
        _mockFileContext.Setup(x => x.AppDirectory).Returns("/app");
        _subject = new GetEmailTemplateQuery(_mockFileContext.Object);
    }

    [Fact]
    public async Task ExecuteAsync_loads_template_from_file_context()
    {
        var expectedPath = "/app/EmailHtmlTemplates/Premailed/welcomeTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(expectedPath)).ReturnsAsync("<html>template</html>");

        var args = new GetEmailTemplateQueryArgs("welcome", new Dictionary<string, string>());

        await _subject.ExecuteAsync(args);

        _mockFileContext.Verify(x => x.ReadAllText(expectedPath), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_throws_when_template_not_found()
    {
        _mockFileContext.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

        var args = new GetEmailTemplateQueryArgs("missing", new Dictionary<string, string>());

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*missing*");
    }

    [Fact]
    public async Task ExecuteAsync_replaces_substitution_placeholders()
    {
        var templatePath = "/app/EmailHtmlTemplates/Premailed/greetingTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(templatePath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(templatePath)).ReturnsAsync("Hello $name$, welcome to $org$");

        var substitutions = new Dictionary<string, string> { { "name", "John" }, { "org", "UKSF" } };
        var args = new GetEmailTemplateQueryArgs("greeting", substitutions);

        var result = await _subject.ExecuteAsync(args);

        result.Should().Contain("Hello John, welcome to UKSF");
    }

    [Fact]
    public async Task ExecuteAsync_adds_randomness_substitution()
    {
        var templatePath = "/app/EmailHtmlTemplates/Premailed/randomTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(templatePath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(templatePath)).ReturnsAsync("value=$randomness$");

        var args = new GetEmailTemplateQueryArgs("random", new Dictionary<string, string>());

        var result = await _subject.ExecuteAsync(args);

        result.Should().NotContain("$randomness$");
        result.Should().StartWith("value=");
        result.Replace("value=", "").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_caches_template_on_second_call()
    {
        var templatePath = "/app/EmailHtmlTemplates/Premailed/cachedTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(templatePath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(templatePath)).ReturnsAsync("<html>cached</html>");

        var args1 = new GetEmailTemplateQueryArgs("cached", new Dictionary<string, string>());
        var args2 = new GetEmailTemplateQueryArgs("cached", new Dictionary<string, string>());

        await _subject.ExecuteAsync(args1);
        await _subject.ExecuteAsync(args2);

        _mockFileContext.Verify(x => x.ReadAllText(templatePath), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_loads_different_templates_separately()
    {
        var pathA = "/app/EmailHtmlTemplates/Premailed/alphaTemplatePremailed.html";
        var pathB = "/app/EmailHtmlTemplates/Premailed/betaTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(pathA)).Returns(true);
        _mockFileContext.Setup(x => x.Exists(pathB)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(pathA)).ReturnsAsync("<html>alpha</html>");
        _mockFileContext.Setup(x => x.ReadAllText(pathB)).ReturnsAsync("<html>beta</html>");

        await _subject.ExecuteAsync(new GetEmailTemplateQueryArgs("alpha", new Dictionary<string, string>()));
        await _subject.ExecuteAsync(new GetEmailTemplateQueryArgs("beta", new Dictionary<string, string>()));

        _mockFileContext.Verify(x => x.ReadAllText(pathA), Times.Once);
        _mockFileContext.Verify(x => x.ReadAllText(pathB), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_modify_original_substitutions()
    {
        var templatePath = "/app/EmailHtmlTemplates/Premailed/safeTemplatePremailed.html";
        _mockFileContext.Setup(x => x.Exists(templatePath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(templatePath)).ReturnsAsync("<html>safe</html>");

        var substitutions = new Dictionary<string, string> { { "key", "value" } };
        var args = new GetEmailTemplateQueryArgs("safe", substitutions);

        await _subject.ExecuteAsync(args);

        substitutions.Should().HaveCount(1);
        substitutions.Should().ContainKey("key");
        substitutions.Should().NotContainKey("randomness");
    }

    [Fact]
    public async Task ExecuteAsync_constructs_correct_file_path()
    {
        var args = new GetEmailTemplateQueryArgs("notification", new Dictionary<string, string>());
        var expectedPath = "/app/EmailHtmlTemplates/Premailed/notificationTemplatePremailed.html";

        _mockFileContext.Setup(x => x.Exists(expectedPath)).Returns(true);
        _mockFileContext.Setup(x => x.ReadAllText(expectedPath)).ReturnsAsync("<html></html>");

        await _subject.ExecuteAsync(args);

        _mockFileContext.Verify(x => x.Exists(expectedPath), Times.Once);
    }
}
