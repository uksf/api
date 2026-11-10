using System;
using System.IO;
using System.Text;
using FluentAssertions;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class ArmaServerExecutableTests
{
    [Fact]
    public void FileName_matches_host_os()
    {
        ArmaServerExecutable.FileName.Should().Be(OperatingSystem.IsWindows() ? "arma3server_x64.exe" : "arma3server_x64");
    }

    [Fact]
    public void ReadGameVersion_reads_console_banner_from_linux_binary()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"arma3server_x64-{Guid.NewGuid():N}");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes("\u007fELF\0\0junk\0Arma 3 Console version 2.22.154089 x64 : port %d\0tail"));
        try
        {
            ArmaServerExecutable.ReadGameVersion(path).Should().Be("2.22");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
