using FluentAssertions;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class AcreExtensionCommandsTests
{
    [Fact]
    public void For_Should_BuildWithMsvc_When_HostIsWindows()
    {
        AcreExtensionCommands.For(true)
                             .Should()
                             .Equal(
                                 new AcreExtensionCommand("cmake", "-S . -B vcproj64 -A x64"),
                                 new AcreExtensionCommand("cmake", "--build vcproj64 --config RelWithDebInfo --target acre Arma2TS ACRE2Steam ACRE2TS"),
                                 new AcreExtensionCommand("cmake", "-S . -B vcproj -A Win32"),
                                 new AcreExtensionCommand("cmake", "--build vcproj --config RelWithDebInfo --target acre Arma2TS ACRE2Steam ACRE2TS Wav2B64")
                             );
    }

    [Fact]
    public void For_Should_CrossBuildWithClangCl_When_HostIsLinux()
    {
        AcreExtensionCommands.For(false)
                             .Should()
                             .Equal(
                                 new AcreExtensionCommand(
                                     "cmake",
                                     "-S . -B xbuild64 -G Ninja -DCMAKE_TOOLCHAIN_FILE=CMakeModules/clang-cl-xwin.cmake -DUSE_64BIT_BUILD=ON"
                                 ),
                                 new AcreExtensionCommand("cmake", "--build xbuild64 --target acre Arma2TS ACRE2Steam ACRE2TS"),
                                 new AcreExtensionCommand(
                                     "cmake",
                                     "-S . -B xbuild -G Ninja -DCMAKE_TOOLCHAIN_FILE=CMakeModules/clang-cl-xwin.cmake -DUSE_64BIT_BUILD=OFF"
                                 ),
                                 new AcreExtensionCommand("cmake", "--build xbuild --target acre Arma2TS ACRE2Steam ACRE2TS Wav2B64")
                             );
    }
}
