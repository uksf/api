using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UKSF.Api.Modpack.BuildProcess.Steps.Common;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class AddonFileCaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"addon-case-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public void LowercaseAddons_renames_addons_folder_and_files_and_keeps_contents()
    {
        Write("@JBad/Addons/Afghan_Houses.pbo", "pbo");
        Write("@JBad/Addons/Afghan_Houses.pbo.Jbad.bisign", "sig");
        Write("@JBad/Key/Jbad.bikey", "key");
        Write("@uksf/addons/uksf_main.pbo", "main");
        Write("@uksf/UKSFTemplate.VR/mission.sqm", "mission");

        var renamed = AddonFileCase.LowercaseAddons(_root);

        renamed.Select(x => x.Replace('\\', '/'))
               .Should()
               .BeEquivalentTo("@JBad/addons", "@JBad/addons/afghan_houses.pbo", "@JBad/addons/afghan_houses.pbo.jbad.bisign");
        Names("@JBad").Should().BeEquivalentTo("addons", "Key");
        Names("@JBad/addons").Should().BeEquivalentTo("afghan_houses.pbo", "afghan_houses.pbo.jbad.bisign");
        File.ReadAllText(Path.Combine(_root, "@JBad", "addons", "afghan_houses.pbo")).Should().Be("pbo");
        Names("@uksf").Should().BeEquivalentTo("addons", "UKSFTemplate.VR");
    }

    [Fact]
    public void LowercaseAddons_is_a_no_op_when_already_lowercase()
    {
        Write("@uksf/addons/uksf_main.pbo", "main");

        AddonFileCase.LowercaseAddons(_root).Should().BeEmpty();
        AddonFileCase.LowercaseAddons(_root).Should().BeEmpty();
    }

    private string[] Names(string relative)
    {
        return Directory.EnumerateFileSystemEntries(Path.Combine(_root, relative)).Select(Path.GetFileName).ToArray();
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
