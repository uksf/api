using FluentAssertions;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class ExtensionTargetsTests
{
    [Fact]
    public void For_Should_BuildDllWithMsvcAndCrossBuildSo_When_HostIsWindows()
    {
        ExtensionTargets.For(true)
                        .Should()
                        .Equal(
                            new ExtensionTarget(
                                "cmd.exe",
                                "/c \"..\\tools\\build_extension.bat --release\"",
                                Path.Join("target", "release", "uksf_x64.dll"),
                                "uksf_x64.dll"
                            ),
                            new ExtensionTarget(
                                "cargo",
                                "zigbuild --release --target x86_64-unknown-linux-gnu.2.28",
                                Path.Join("target", "x86_64-unknown-linux-gnu", "release", "libuksf_x64.so"),
                                "uksf_x64.so"
                            )
                        );
    }

    [Fact]
    public void For_Should_CrossBuildDllWithXwin_When_HostIsLinux()
    {
        ExtensionTargets.For(false)
                        .Should()
                        .Equal(
                            new ExtensionTarget(
                                "cargo",
                                "xwin build --release --target x86_64-pc-windows-msvc",
                                Path.Join("target", "x86_64-pc-windows-msvc", "release", "uksf_x64.dll"),
                                "uksf_x64.dll"
                            ),
                            new ExtensionTarget(
                                "cargo",
                                "zigbuild --release --target x86_64-unknown-linux-gnu.2.28",
                                Path.Join("target", "x86_64-unknown-linux-gnu", "release", "libuksf_x64.so"),
                                "uksf_x64.so"
                            )
                        );
    }
}
