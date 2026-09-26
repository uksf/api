using FluentAssertions;
using Moq;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModPboDiscoveryTests
{
    private const string ModPath = "C:\\mod";
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    private List<WorkshopModPboDiscovery.PboFile> Find(params string[] relativePaths)
    {
        _fileSystemService.Setup(x => x.DirectoryExists(ModPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(ModPath, "*.pbo", SearchOption.AllDirectories))
                          .Returns(relativePaths.Select(x => Path.Combine(ModPath, x)).ToList());
        return WorkshopModPboDiscovery.Find(_fileSystemService.Object, ModPath);
    }

    [Fact]
    public void Find_ShouldMarkPbosUnderTopLevelAddonsAsAddonsWhateverTheCase()
    {
        var result = Find("Addons\\a.pbo", "addons\\sub\\b.pbo");

        result.Should().OnlyContain(x => x.InAddons);
        result.Select(x => x.Folder).Should().BeEquivalentTo("Addons", "addons/sub");
    }

    [Fact]
    public void Find_ShouldReturnPbosInOtherFoldersWithTheirFolder()
    {
        var result = Find("addons\\a.pbo", "optionals\\Addons\\a_ace.pbo", "root.pbo");

        result.Where(x => !x.InAddons).Select(x => (x.Name, x.Folder)).Should().BeEquivalentTo([("a_ace.pbo", "optionals/Addons"), ("root.pbo", "")]);
    }

    [Fact]
    public void Find_ShouldLeaveOutAPboOutsideAddonsThatSharesANameWithOneInside()
    {
        var result = Find("addons\\a.pbo", "optionals\\a.pbo");

        result.Should().ContainSingle().Which.Folder.Should().Be("addons");
    }

    [Fact]
    public void Find_ShouldLeaveOutANameFoundInMoreThanOneFolderOutsideAddons()
    {
        var result = Find("addons\\a.pbo", "optionals\\rhs\\b.pbo", "optionals\\cup\\B.pbo");

        result.Select(x => x.Name).Should().BeEquivalentTo("a.pbo");
    }

    [Fact]
    public void Find_WhenDuplicateNamesInsideAddons_ShouldThrow()
    {
        var action = () => Find("addons\\a.pbo", "addons\\sub\\a.pbo");

        action.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate PBO names*");
    }

    [Fact]
    public void Find_WhenModDirectoryMissing_ShouldReturnEmpty()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(ModPath)).Returns(false);

        WorkshopModPboDiscovery.Find(_fileSystemService.Object, ModPath).Should().BeEmpty();
    }
}
