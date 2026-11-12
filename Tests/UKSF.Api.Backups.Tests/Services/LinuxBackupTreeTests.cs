using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class LinuxBackupTreeTests
{
    private readonly Mock<IFileSystemProvider> _mockFileSystemProvider = new();

    public LinuxBackupTreeTests()
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.GetFiles(It.IsAny<string>())).Returns([]);
        _mockFileSystemProvider.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
    }

    private void GivenTree(string path, IEnumerable<string> directories, IEnumerable<string> files)
    {
        _mockFileSystemProvider.Setup(x => x.GetDirectories(path)).Returns(directories.ToList());
        _mockFileSystemProvider.Setup(x => x.GetFiles(path)).Returns(files.ToList());
    }

    [LinuxFact]
    public void The_only_root_is_slash()
    {
        _mockFileSystemProvider.Setup(x => x.GetDrives()).Returns(["/"]);

        new FileTreeService(_mockFileSystemProvider.Object).GetRoots().Select(x => x.Path).Should().Equal("/");
    }

    [LinuxFact]
    public void Children_of_a_unix_folder_list_directories_before_files()
    {
        GivenTree("/srv", ["/srv/teamspeak", "/srv/arma"], ["/srv/notes.txt", "/srv/Deets.txt"]);

        var result = new FileTreeService(_mockFileSystemProvider.Object).GetChildren("/srv/").ToList();

        result.Select(x => x.Name).Should().Equal("arma", "teamspeak", "Deets.txt", "notes.txt");
    }

    [LinuxFact]
    public void A_unix_folder_is_walked_and_an_exclude_only_matches_its_exact_case()
    {
        GivenTree("/srv/nginx", ["/srv/nginx/logs", "/srv/nginx/Logs"], ["/srv/nginx/nginx.conf"]);
        GivenTree("/srv/nginx/logs", [], ["/srv/nginx/logs/access.log"]);
        GivenTree("/srv/nginx/Logs", [], ["/srv/nginx/Logs/keep.log"]);

        var result = new BackupFileWalker(_mockFileSystemProvider.Object).Walk(
            [
                new DomainBackupEntry
                {
                    Path = "/srv/nginx",
                    EntryType = BackupEntryType.Folder,
                    Excludes = ["/srv/nginx/logs"]
                }
            ]
        );

        result.Files.Select(x => x.EntryName).Should().BeEquivalentTo(["files/srv/nginx/nginx.conf", "files/srv/nginx/Logs/keep.log"]);
    }
}
