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

public class NpcVoiceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"npc-voice-{Guid.NewGuid():N}");
    private readonly NpcVoiceStore _sut;

    public NpcVoiceStoreTests()
    {
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_VOICE_PATH")).Returns(new DomainVariableItem { Key = "NPC_VOICE_PATH", Item = _root });
        _sut = new NpcVoiceStore(variables.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Save_then_read_round_trips_and_returns_relative_path()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var relativePath = await _sut.SaveAsync("smuggler", bytes);
        relativePath.Should().Be("smuggler.wav");
        (await _sut.ReadAsync(relativePath)).Should().Equal(bytes);
    }

    [Fact]
    public async Task Delete_removes_the_master_file()
    {
        var relativePath = await _sut.SaveAsync("smuggler", new byte[] { 9 });
        _sut.Delete(relativePath);
        (await _sut.ReadAsync(relativePath)).Should().BeNull();
    }

    [Fact]
    public async Task Read_missing_file_returns_null()
    {
        (await _sut.ReadAsync("nope.wav")).Should().BeNull();
    }
}
