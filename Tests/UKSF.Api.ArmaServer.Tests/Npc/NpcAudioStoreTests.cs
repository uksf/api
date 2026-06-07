using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcAudioStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"npc-audio-{Guid.NewGuid():N}");
    private readonly NpcAudioStore _sut;

    public NpcAudioStoreTests()
    {
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_AUDIO_PATH")).Returns(new DomainVariableItem { Key = "NPC_AUDIO_PATH", Item = _root });
        _sut = new NpcAudioStore(variables.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task SaveAsync_WritesUnderDateFolder_AndReturnsRelativePath()
    {
        var relative = await _sut.SaveAsync("session1", "npc1", "ammo", [1, 2, 3]);

        var expectedFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
        relative.Should().Be($"{expectedFolder}/session1_npc1_ammo.wav");
        File.Exists(Path.Combine(_root, expectedFolder, "session1_npc1_ammo.wav")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_SanitisesPathHostileComponents()
    {
        var relative = await _sut.SaveAsync("../evil", "a/b", "c\\d", [1]);

        relative.Should().NotContain("..");
        relative.Should().EndWith("__evil_a_b_c_d.wav");
        Directory.GetFiles(_root, "*.wav", SearchOption.AllDirectories).Should().HaveCount(1);
    }

    [Fact]
    public async Task ReadAsync_RoundTrips()
    {
        var relative = await _sut.SaveAsync("s", "n", "f0", [9, 8, 7]);
        var bytes = await _sut.ReadAsync(relative);
        bytes.Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsNull()
    {
        var bytes = await _sut.ReadAsync("2026-01-01/nope.wav");
        bytes.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NoRootConfigured_Throws()
    {
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_AUDIO_PATH")).Returns((DomainVariableItem)null);
        var sut = new NpcAudioStore(variables.Object);

        await sut.Invoking(x => x.SaveAsync("s", "n", "c", [1])).Should().ThrowAsync<InvalidOperationException>();
    }
}
