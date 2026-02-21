using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class CommentThreadServiceTests
{
    private readonly Mock<ICommentThreadContext> _mockCommentThreadContext = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly CommentThreadService _subject;

    public CommentThreadServiceTests()
    {
        _subject = new CommentThreadService(_mockCommentThreadContext.Object, _mockDisplayNameService.Object);
    }

    [Fact]
    public void GetCommentThreadComments_returns_reversed_comments()
    {
        var comment1 = new DomainComment { Id = "c1", Content = "First" };
        var comment2 = new DomainComment { Id = "c2", Content = "Second" };
        var comment3 = new DomainComment { Id = "c3", Content = "Third" };
        var thread = new DomainCommentThread { Id = "thread1", Comments = [comment1, comment2, comment3] };
        _mockCommentThreadContext.Setup(x => x.GetSingle("thread1")).Returns(thread);

        var result = _subject.GetCommentThreadComments("thread1").ToList();

        result.Should().HaveCount(3);
        result[0].Id.Should().Be("c3");
        result[1].Id.Should().Be("c2");
        result[2].Id.Should().Be("c1");
    }

    [Fact]
    public void GetCommentThreadComments_returns_empty_when_thread_not_found()
    {
        _mockCommentThreadContext.Setup(x => x.GetSingle("missing")).Returns((DomainCommentThread)null);

        var result = _subject.GetCommentThreadComments("missing").ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommentThreadParticipants_returns_union_of_comment_authors_and_thread_authors()
    {
        var thread = new DomainCommentThread
        {
            Id = "thread1",
            Comments = [new DomainComment { Author = "author1" }, new DomainComment { Author = "author2" }],
            Authors = ["author3", "author4"]
        };
        _mockCommentThreadContext.Setup(x => x.GetSingle("thread1")).Returns(thread);

        var result = _subject.GetCommentThreadParticipants("thread1").ToList();

        result.Should().BeEquivalentTo(["author1", "author2", "author3", "author4"]);
    }

    [Fact]
    public void GetCommentThreadParticipants_deduplicates_authors()
    {
        var thread = new DomainCommentThread
        {
            Id = "thread1",
            Comments = [new DomainComment { Author = "author1" }, new DomainComment { Author = "author1" }],
            Authors = ["author1", "author2"]
        };
        _mockCommentThreadContext.Setup(x => x.GetSingle("thread1")).Returns(thread);

        var result = _subject.GetCommentThreadParticipants("thread1").ToList();

        result.Should().BeEquivalentTo(["author1", "author2"]);
    }

    [Fact]
    public void GetCommentThreadParticipants_returns_empty_when_thread_not_found()
    {
        _mockCommentThreadContext.Setup(x => x.GetSingle("missing")).Returns((DomainCommentThread)null);

        var result = _subject.GetCommentThreadParticipants("missing").ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void FormatComment_maps_author_id_to_display_name()
    {
        var timestamp = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var comment = new DomainComment
        {
            Id = "c1",
            Author = "author1",
            Content = "Hello",
            Timestamp = timestamp
        };
        _mockDisplayNameService.Setup(x => x.GetDisplayName("author1")).Returns("John Smith");

        var result = _subject.FormatComment(comment);

        var type = result.GetType();
        type.GetProperty("Id")!.GetValue(result).Should().Be("c1");
        type.GetProperty("Author")!.GetValue(result).Should().Be("author1");
        type.GetProperty("Content")!.GetValue(result).Should().Be("Hello");
        type.GetProperty("DisplayName")!.GetValue(result).Should().Be("John Smith");
        type.GetProperty("Timestamp")!.GetValue(result).Should().Be(timestamp);
    }
}
