namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

public record AcreExtensionCommand(string Executable, string Arguments);

public static class AcreExtensionCommands
{
    private const string Cmake = "cmake";
    private const string Toolchain = "CMakeModules/clang-cl-xwin.cmake";
    private static readonly string[] Targets64 = ["acre", "Arma2TS", "ACRE2Steam", "ACRE2TS"];
    private static readonly string[] Targets32 = [.. Targets64, "Wav2B64"];

    public static IReadOnlyList<AcreExtensionCommand> For(bool windowsHost)
    {
        return windowsHost
            ? [.. Msvc("vcproj64", "x64", Targets64), .. Msvc("vcproj", "Win32", Targets32)]
            : [.. ClangCl("xbuild64", "ON", Targets64), .. ClangCl("xbuild", "OFF", Targets32)];
    }

    private static AcreExtensionCommand[] Msvc(string buildDirectory, string platform, string[] targets)
    {
        return
        [
            new(Cmake, $"-S . -B {buildDirectory} -A {platform}"),
            new(Cmake, $"--build {buildDirectory} --config RelWithDebInfo --target {string.Join(' ', targets)}")
        ];
    }

    private static AcreExtensionCommand[] ClangCl(string buildDirectory, string use64Bit, string[] targets)
    {
        return
        [
            new(Cmake, $"-S . -B {buildDirectory} -G Ninja -DCMAKE_TOOLCHAIN_FILE={Toolchain} -DUSE_64BIT_BUILD={use64Bit}"),
            new(Cmake, $"--build {buildDirectory} --target {string.Join(' ', targets)}")
        ];
    }
}
