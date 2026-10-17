using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Tests.Helpers;

public sealed class MissionPatchingFixture : IDisposable
{
    private static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

    private readonly string _tempDir = NewTempDir("uksf_integration");
    private readonly string _backupDir = NewTempDir("uksf_backup");
    private readonly string _capturedDir = NewTempDir("uksf_captured");
    private readonly MissionPatchingService _service;

    public MissionPatchingFixture()
    {
        ModsDir = NewTempDir("uksf_mods");
        VariablesService.Setup(x => x.GetVariable("MISSIONS_BACKUPS")).Returns(new DomainVariableItem { Key = "MISSIONS_BACKUPS", Item = _backupDir });
        VariablesService.Setup(x => x.GetVariable("MISSIONS_WORKING_DIR")).Returns(new DomainVariableItem { Key = "MISSIONS_WORKING_DIR", Item = _tempDir });

        var decompiler = new Mock<ISqmDecompiler>();
        decompiler.Setup(x => x.IsBinarized(It.IsAny<string>())).ReturnsAsync(false);

        _service = new MissionPatchingService(
            new PboHandler(PboTools.Object, VariablesService.Object),
            new SqmReader(),
            new SqmWriter(),
            new SqmPatcher(),
            new DescriptionReader(),
            new DescriptionWriter(),
            new DescriptionPatcher(),
            new SettingsReader(),
            TestData.BuildPatchDataBuilder(),
            decompiler.Object,
            new HeadlessClientPatcher(VariablesService.Object),
            new DebugConsoleStripper(),
            new Mock<IUksfLogger>().Object
        );
    }

    public Mock<IVariablesService> VariablesService { get; } = new();
    public Mock<IPboTools> PboTools { get; } = new();
    public TestPatchDataBuilder TestData { get; } = new();
    public string ModsDir { get; }
    public string PboPath => Path.Combine(_tempDir, "testmission.Altis.pbo");

    public void Dispose()
    {
        foreach (var dir in new[] { _tempDir, _backupDir, ModsDir, _capturedDir })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public Task<MissionPatchingResult> Patch(int defaultMaxCurators = 5) => _service.PatchMission(PboPath, ModsDir, defaultMaxCurators);

    public string SetupMission(
        string baseScenario,
        string descOverride = null,
        string sqmOverride = null,
        string cbaOverride = null,
        Action<string> preExtractAction = null
    )
    {
        File.WriteAllText(PboPath, "fake pbo content");
        var folderPath = Path.Combine(_tempDir, "testmission.Altis");

        PboTools.Setup(x => x.ExtractPbo(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback(() =>
                    {
                        Directory.CreateDirectory(folderPath);
                        foreach (var file in Directory.GetFiles(ScenarioDir(baseScenario)))
                        {
                            File.Copy(file, Path.Combine(folderPath, Path.GetFileName(file)), true);
                        }

                        CopyOverride(sqmOverride, "mission.sqm", folderPath);
                        CopyOverride(descOverride, "description.ext", folderPath);
                        CopyOverride(cbaOverride, "cba_settings.sqf", folderPath);
                        preExtractAction?.Invoke(folderPath);
                    }
                );

        PboTools.Setup(x => x.MakePbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string, string>((folder, _, _) => CaptureFolder(folder));
        PboTools.Setup(x => x.SimplePackPbo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string, string>((folder, _, _) => CaptureFolder(folder));

        return folderPath;
    }

    public List<string> ReadCapturedFile(string fileName) => File.ReadAllLines(CapturedPath(fileName)).ToList();

    public bool CapturedFileExists(string fileName) => File.Exists(Path.Combine(_capturedDir, fileName));

    public byte[] ReadCapturedBytes(string fileName) => File.ReadAllBytes(CapturedPath(fileName));

    public static byte[] ReadTestDataBytes(string scenario, string fileName) => File.ReadAllBytes(Path.Combine(ScenarioDir(scenario), fileName));

    private string CapturedPath(string fileName)
    {
        var path = Path.Combine(_capturedDir, fileName);
        File.Exists(path).Should().BeTrue($"captured file '{fileName}' should exist");
        return path;
    }

    private void CaptureFolder(string sourceFolder)
    {
        foreach (var file in Directory.GetFiles(sourceFolder))
        {
            File.Copy(file, Path.Combine(_capturedDir, Path.GetFileName(file)), true);
        }
    }

    private static void CopyOverride(string scenario, string fileName, string targetFolder)
    {
        if (scenario is not null)
        {
            File.Copy(Path.Combine(ScenarioDir(scenario), fileName), Path.Combine(targetFolder, fileName), true);
        }
    }

    private static string ScenarioDir(string scenario)
    {
        var dir = Path.Combine(TestDataDir, scenario);
        return Directory.Exists(dir) ? dir : throw new DirectoryNotFoundException($"Test data directory not found: {dir}");
    }

    private static string NewTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
