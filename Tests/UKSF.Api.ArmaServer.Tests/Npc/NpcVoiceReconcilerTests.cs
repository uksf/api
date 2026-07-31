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
    private readonly NpcVoiceReconciler _sut;

    public NpcVoiceReconcilerTests()
    {
        var variables = new Mock<IVariablesService>();
        variables.Setup(x => x.GetVariable("NPC_VOICE_PATH")).Returns(new DomainVariableItem { Item = _root });
        var store = new NpcVoiceStore(variables.Object);
        _sut = new NpcVoiceReconciler(_voices.Object, store, variables.Object, new Mock<IUksfLogger>().Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void WriteVoice(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, [1, 2, 3]);
    }

    [Fact]
    public async Task Registers_A_Voice_That_Is_On_Disk_But_Missing_From_The_Registry()
    {
        WriteVoice("merl/ref.wav");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl" && v.MoodOf == null && v.FilePath == "merl/ref.wav")), Times.Once);
    }

    [Fact]
    public async Task Links_A_Mood_Variant_Back_To_Its_Base()
    {
        WriteVoice("merl/ref.wav");
        WriteVoice("merl/angry.wav");
        WriteVoice("merl/neutral.wav");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl_angry" && v.MoodOf == "merl" && v.FilePath == "merl/angry.wav")), Times.Once);
        _voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "merl_neutral" && v.MoodOf == "merl")), Times.Once);
    }

    [Fact]
    public async Task Ignores_Filler_Files()
    {
        WriteVoice("merl/ref.wav");
        WriteVoice("merl/fillers/Umm.wav");

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.IsAny<DomainNpcVoice>()), Times.Once); // only the base voice
    }

    [Fact]
    public async Task Repoints_A_Doc_Whose_File_Moved_To_The_Folder_Layout()
    {
        WriteVoice("merl/ref.wav");
        var stale = new DomainNpcVoice { VoiceId = "merl", FilePath = "merl.wav" };
        _voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns(stale);

        await _sut.StartAsync(CancellationToken.None);

        stale.FilePath.Should().Be("merl/ref.wav");
        _voices.Verify(x => x.Replace(stale), Times.Once);
        _voices.Verify(x => x.Add(It.IsAny<DomainNpcVoice>()), Times.Never);
    }

    [Fact]
    public async Task Leaves_A_Happy_Doc_Alone()
    {
        WriteVoice("merl/ref.wav");
        _voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns(new DomainNpcVoice { VoiceId = "merl", FilePath = "merl/ref.wav" });

        await _sut.StartAsync(CancellationToken.None);

        _voices.Verify(x => x.Add(It.IsAny<DomainNpcVoice>()), Times.Never);
        _voices.Verify(x => x.Replace(It.IsAny<DomainNpcVoice>()), Times.Never);
    }
}
