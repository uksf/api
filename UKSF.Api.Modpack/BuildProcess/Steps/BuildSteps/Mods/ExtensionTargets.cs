namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

public record ExtensionTarget(string Executable, string Arguments, string BuiltPath, string FileName);

public static class ExtensionTargets
{
    private const string LinuxTarget = "x86_64-unknown-linux-gnu";
    private const string LinuxGlibc = "2.28";
    private const string WindowsTarget = "x86_64-pc-windows-msvc";

    private static readonly ExtensionTarget LinuxSo = new(
        "cargo",
        $"zigbuild --release --target {LinuxTarget}.{LinuxGlibc}",
        Path.Join("target", LinuxTarget, "release", "libuksf_x64.so"),
        "uksf_x64.so"
    );

    public static IReadOnlyList<ExtensionTarget> For(bool windowsHost)
    {
        var dll = windowsHost
            ? new ExtensionTarget("cmd.exe", "/c \"..\\tools\\build_extension.bat --release\"", Path.Join("target", "release", "uksf_x64.dll"), "uksf_x64.dll")
            : new ExtensionTarget(
                "cargo",
                $"xwin build --release --target {WindowsTarget}",
                Path.Join("target", WindowsTarget, "release", "uksf_x64.dll"),
                "uksf_x64.dll"
            );
        return [dll, LinuxSo];
    }
}
