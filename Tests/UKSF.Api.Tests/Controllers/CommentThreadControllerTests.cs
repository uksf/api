using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class CommentThreadControllerTests
{
    private readonly Mock<ICommentThreadService> _mockCommentThreadService = new();
    private readonly CommentThreadController _controller;

    public CommentThreadControllerTests()
    {
        _controller = new CommentThreadController(
            new Mock<IAccountContext>().Object,
            new Mock<ICommentThreadContext>().Object,
            _mockCommentThreadService.Object,
            new Mock<IRanksService>().Object,
            new Mock<IAccountService>().Object,
            new Mock<IDisplayNameService>().Object,
            new Mock<IRecruitmentService>().Object,
            new Mock<INotificationsService>().Object,
            new Mock<IHttpContextService>().Object
        );
    }

    [Fact]
    public async Task DeleteComment_should_not_throw_when_comment_exists()
    {
        var comment = new DomainComment
        {
            Id = "comment1",
            Content = "test",
            Author = "author1"
        };
        _mockCommentThreadService.Setup(x => x.GetCommentThreadComments("thread1")).Returns([comment]);

        await _controller.Invoking(c => c.DeleteComment("thread1", "comment1")).Should().NotThrowAsync();

        _mockCommentThreadService.Verify(x => x.RemoveComment("thread1", comment), Times.Once);
    }

    [Fact]
    public async Task DeleteComment_should_not_call_remove_when_comment_not_found()
    {
        var comment = new DomainComment
        {
            Id = "comment1",
            Content = "test",
            Author = "author1"
        };
        _mockCommentThreadService.Setup(x => x.GetCommentThreadComments("thread1")).Returns([comment]);

        // Passing a non-existent comment ID should not attempt to remove null
        await _controller.Invoking(c => c.DeleteComment("thread1", "nonexistent")).Should().NotThrowAsync();

        _mockCommentThreadService.Verify(x => x.RemoveComment(It.IsAny<string>(), It.IsAny<DomainComment>()), Times.Never);
    }
}
