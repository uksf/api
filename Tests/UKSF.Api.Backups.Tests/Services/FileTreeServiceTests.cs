using System;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class FileTreeServiceTests
{
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();
    private readonly FileTreeService _subject;

    public FileTreeServiceTests()
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.GetFiles(It.IsAny<string>())).Returns([]);
        _subject = new FileTreeService(_mockFileSystemProvider.Object);
    }

    [Fact]
    public void Roots_are_the_ready_drives_sorted()
    {
        _mockFileSystemProvider.Setup(x => x.GetDrives()).Returns([@"E:\", @"C:\", @"D:\"]);

        var result = _subject.GetRoots().ToList();

        result.Select(x => x.Path).Should().ContainInOrder(@"C:\", @"D:\", @"E:\");
        result.Should().OnlyContain(x => x.IsDirectory && x.HasChildren);
    }

    [Fact]
    public void Children_list_directories_before_files_each_sorted()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(@"C:\Server")).Returns(true);
        _mockFileSystemProvider.Setup(x => x.GetDirectories(@"C:\Server")).Returns([@"C:\Server\Teamspeak", @"C:\Server\Arma"]);
        _mockFileSystemProvider.Setup(x => x.GetFiles(@"C:\Server")).Returns([@"C:\Server\notes.txt", @"C:\Server\deets.txt"]);

        var result = _subject.GetChildren(@"C:/Server/").ToList();

        result.Select(x => x.Name).Should().ContainInOrder("Arma", "Teamspeak", "deets.txt", "notes.txt");
        result.Take(2).Should().OnlyContain(x => x.IsDirectory);
        result.Skip(2).Should().OnlyContain(x => !x.IsDirectory && !x.HasChildren);
    }

    [Fact]
    public void An_empty_directory_reports_no_children()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(@"C:\Server")).Returns(true);
        _mockFileSystemProvider.Setup(x => x.GetDirectories(@"C:\Server")).Returns([@"C:\Server\Empty"]);

        var result = _subject.GetChildren(@"C:\Server").ToList();

        result.Should().ContainSingle().Which.HasChildren.Should().BeFalse();
    }

    [Fact]
    public void A_directory_that_denies_access_is_listed_as_empty_rather_than_failing()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(@"C:\Server")).Returns(true);
        _mockFileSystemProvider.Setup(x => x.GetDirectories(@"C:\Server")).Throws<UnauthorizedAccessException>();
        _mockFileSystemProvider.Setup(x => x.GetFiles(@"C:\Server")).Throws<UnauthorizedAccessException>();

        _subject.GetChildren(@"C:\Server").Should().BeEmpty();
    }

    [Fact]
    public void An_unknown_directory_is_a_not_found()
    {
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        var act = () => _subject.GetChildren(@"C:\Nope").ToList();

        act.Should().Throw<UksfException>().Which.StatusCode.Should().Be(404);
    }
}
