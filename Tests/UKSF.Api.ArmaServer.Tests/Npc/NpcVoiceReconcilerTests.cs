using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcVoiceReconcilerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"npcvoices-{Guid.NewGuid():N}");
    private readonly Mock<INpcVoicesContext> _voices = new();
    private readonly Mock<INpcVoiceStore> _store = new();
    private readonly NpcVoiceReconciler _sut;

    public NpcVoiceReconcilerTests()
    {
        Directory.CreateDirectory(_root);
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_VOICE_PATH")).Returns(new DomainVariableItem { Item = _root });
        _store.Setup(x => x.ReadAsync(It.IsAny<string>())).ReturnsAsync([1, 2, 3]);
        _sut = new NpcVoiceReconciler(_voices.Object, _store.Object, variables.Object, new Mock<IUksfLogger>().Object);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private void WriteVoice(string voiceId) => File.WriteAllBytes(Path.Combine(_root, $"{voiceId}.wav"), [1, 2, 3]);

    [Fact]
    public async Task Registers_A_Voice_That_Is_On_Disk_But_Missing_From_The_Registry()
    {
        WriteVoice("merl");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl" && v.MoodOf == null && v.FilePath == "merl.wav")), Times.Once);
    }

    [Fact]
    public async Task Links_A_Mood_Variant_Back_To_Its_Base()
    {
        WriteVoice("merl_angry");
        WriteVoice("merl_neutral");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl_angry" && v.MoodOf == "merl")), Times.Once);
        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl_neutral" && v.MoodOf == "merl")), Times.Once);
    }

    [Fact]
    public async Task Treats_An_Underscore_That_Is_Not_A_Mood_As_Part_Of_The_Name()
    {
        WriteVoice("wizard_test");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "wizard_test" && v.MoodOf == null)), Times.Once);
    }

    [Fact]
    public async Task Leaves_An_Already_Registered_Voice_Alone()
    {
        WriteVoice("merl");
        _voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns(new DomainNpcVoice { VoiceId = "merl" });

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.IsAny<DomainNpcVoice>()), Times.Never);
    }
}
