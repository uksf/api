using Moq;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModDependencyFilesServiceTests
{
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly WorkshopModDependencyFilesService _subject;

    private readonly string _workshopModPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
    private readonly string _devAddonsPath = Path.Combine("C:\\dev", "Repo", "@uksf_dependencies", "addons");
    private readonly string _rcAddonsPath = Path.Combine("C:\\rc", "Repo", "@uksf_dependencies", "addons");
    private readonly string _devDependenciesPath = Path.Combine("C:\\dev", "Repo", "@uksf_dependencies");
    private readonly string _rcDependenciesPath = Path.Combine("C:\\rc", "Repo", "@uksf_dependencies");

    public WorkshopModDependencyFilesServiceTests()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _subject = new WorkshopModDependencyFilesService(_variablesService.Object, _fileSystemService.Object);
    }

    private static DomainWorkshopMod CreateWorkshopMod()
    {
        return new DomainWorkshopMod { SteamId = "123", Name = "Test Mod" };
    }

    [Fact]
    public void CopyPbosToDependencies_ShouldCopySelectedPbosToBothRepos()
    {
        var addonsPath = Path.Combine(_workshopModPath, "addons");
        var sourcePbo = Path.Combine(addonsPath, "selected.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(addonsPath, "*.pbo", SearchOption.AllDirectories))
                          .Returns([sourcePbo, Path.Combine(addonsPath, "optional", "other.pbo")]);

        _subject.CopyPbosToDependencies(CreateWorkshopMod(), ["selected.pbo"]);

        _fileSystemService.Verify(x => x.CopyFile(sourcePbo, Path.Combine(_devAddonsPath, "selected.pbo"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourcePbo, Path.Combine(_rcAddonsPath, "selected.pbo"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Exactly(2));
    }

    [Fact]
    public void CopyPbosToDependencies_WhenNoPbosSelected_ShouldNotEnumerateOrCopy()
    {
        _subject.CopyPbosToDependencies(CreateWorkshopMod(), []);

        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void CopyExtensionsToDependencies_ShouldCopyFromModRootToDependenciesRoot()
    {
        _subject.CopyExtensionsToDependencies(CreateWorkshopMod(), ["ctab_connect.dll"]);

        var source = Path.Combine(_workshopModPath, "ctab_connect.dll");
        _fileSystemService.Verify(x => x.CopyFile(source, Path.Combine(_devDependenciesPath, "ctab_connect.dll"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(source, Path.Combine(_rcDependenciesPath, "ctab_connect.dll"), true), Times.Once);
    }

    [Fact]
    public void CopyExtensionsToDependencies_ShouldNotTouchTheAddonsDirectory()
    {
        _subject.CopyExtensionsToDependencies(CreateWorkshopMod(), ["ctab_connect_x64.dll"]);

        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.Is<string>(destination => destination.Contains("addons")), true), Times.Never);
    }

    [Fact]
    public void CopyExtensionsToDependencies_WhenNoFiles_ShouldNotCopy()
    {
        _subject.CopyExtensionsToDependencies(CreateWorkshopMod(), []);

        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DeletePbosFromDependencies_WhenFilesExist_ShouldDelete()
    {
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        _subject.DeletePbosFromDependencies(["test.pbo"]);

        _fileSystemService.Verify(x => x.DeleteFile(Path.Combine(_devAddonsPath, "test.pbo")), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(Path.Combine(_rcAddonsPath, "test.pbo")), Times.Once);
    }

    [Fact]
    public void DeletePbosFromDependencies_WhenFilesDoNotExist_ShouldNotDelete()
    {
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        _subject.DeletePbosFromDependencies(["test.pbo"]);

        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DeleteExtensionsFromDependencies_WhenFilesExist_ShouldDeleteFromDependenciesRoot()
    {
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        _subject.DeleteExtensionsFromDependencies(["ctab_connect.dll"]);

        _fileSystemService.Verify(x => x.DeleteFile(Path.Combine(_devDependenciesPath, "ctab_connect.dll")), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(Path.Combine(_rcDependenciesPath, "ctab_connect.dll")), Times.Once);
    }

    [Fact]
    public void DeleteExtensionsFromDependencies_WhenFilesDoNotExist_ShouldNotDelete()
    {
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        _subject.DeleteExtensionsFromDependencies(["ctab_connect.dll"]);

        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }
}
