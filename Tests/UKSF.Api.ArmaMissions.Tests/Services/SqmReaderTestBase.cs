using System;
using System.IO;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public abstract class SqmReaderTestBase : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"uksf_sqmreader_{Guid.NewGuid():N}");

    protected SqmReaderTestBase() => Directory.CreateDirectory(_tempDir);

    protected SqmReader Subject { get; } = new();

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    protected MissionPatchContext CreateContext() => new() { FolderPath = _tempDir };

    protected void WriteSqm(string content) => File.WriteAllText(Path.Combine(_tempDir, "mission.sqm"), content);
}
