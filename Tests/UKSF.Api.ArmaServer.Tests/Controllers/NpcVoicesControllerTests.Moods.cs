using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public partial class NpcVoicesControllerTests
{
    private static DomainNpcVoice Parent =>
        new()
        {
            Id = "p1",
            VoiceId = "smuggler",
            DisplayName = "Smuggler",
            OwnerId = "user-1",
            FilePath = "smuggler/ref.wav"
        };

    private void GivenVoices(params DomainNpcVoice[] voices)
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((Func<DomainNpcVoice, bool> p) => voices.FirstOrDefault(p));
        _context.Setup(x => x.Get(It.IsAny<Func<DomainNpcVoice, bool>>())).Returns((Func<DomainNpcVoice, bool> p) => voices.Where(p));
        _context.Setup(x => x.Delete(It.IsAny<string>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Mood_variant_with_an_unknown_mood_is_rejected()
    {
        GivenVoices(Parent);
        var act = () => _sut.Upload(WavFile(24000 * 2 * 5), null, "smuggler", "Furious");
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*angry*");
        _store.Verify(x => x.SaveBaseAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Mood_variant_on_someone_elses_voice_is_rejected()
    {
        _httpContext.Setup(x => x.GetUserId()).Returns("someone-else");
        GivenVoices(Parent);
        var act = () => _sut.Upload(WavFile(24000 * 2 * 5), null, "smuggler", "Angry");
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Mood_variant_replaces_the_existing_mood_and_leaves_the_queue()
    {
        var old = new DomainNpcVoice
        {
            Id = "v2",
            VoiceId = "smuggler_angry",
            MoodOf = "smuggler",
            FilePath = "smuggler/angry.wav"
        };
        GivenVoices(Parent, old);
        var job = DomainNpcVoiceJob.NewJob("smuggler", "user-1");
        _jobs.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoiceJob, bool>>())).Returns(job);
        _jobs.Setup(x => x.Replace(It.IsAny<DomainNpcVoiceJob>())).Returns(Task.CompletedTask);

        var result = await _sut.Upload(WavFile(24000 * 2 * 5), null, "smuggler", "Angry");

        result.VoiceId.Should().Be("smuggler_angry");
        _store.Verify(x => x.Delete("smuggler/angry.wav"), Times.Once);
        _context.Verify(x => x.Delete("v2"), Times.Once);
        job.Moods.Select(m => m.Mood).Should().NotContain("angry");
    }

    [Fact]
    public async Task Deleting_a_base_voice_deletes_its_moods_and_job()
    {
        var angry = new DomainNpcVoice
        {
            Id = "v2",
            VoiceId = "smuggler_angry",
            MoodOf = "smuggler",
            FilePath = "smuggler/angry.wav"
        };
        GivenVoices(Parent, angry);
        _context.Setup(x => x.GetSingle("p1")).Returns(Parent);
        _jobs.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoiceJob, bool>>())).Returns(new DomainNpcVoiceJob { Id = "j1", BaseVoiceId = "smuggler" });
        _jobs.Setup(x => x.Delete("j1")).Returns(Task.CompletedTask);

        var result = await _sut.Delete("p1");

        result.Should().BeOfType<OkResult>();
        _context.Verify(x => x.Delete("v2"), Times.Once);
        _context.Verify(x => x.Delete("p1"), Times.Once);
        _store.Verify(x => x.Delete("smuggler/angry.wav"), Times.Once);
        _jobs.Verify(x => x.Delete("j1"), Times.Once);
    }

    [Fact]
    public async Task GenerateMoods_without_a_list_queues_only_missing_moods()
    {
        GivenVoices(Parent, new DomainNpcVoice { VoiceId = "smuggler_angry", MoodOf = "smuggler" });
        _jobs.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoiceJob, bool>>())).Returns((DomainNpcVoiceJob)null);
        _jobs.Setup(x => x.Add(It.IsAny<DomainNpcVoiceJob>())).Returns(Task.CompletedTask);

        var job = await _sut.GenerateMoods("smuggler");

        job.Moods.Select(m => m.Mood).Should().BeEquivalentTo("neutral", "afraid", "sad", "happy");
    }

    [Fact]
    public async Task GenerateMoods_with_a_list_merges_into_the_existing_job()
    {
        GivenVoices(Parent);
        var job = DomainNpcVoiceJob.NewJob("smuggler", "user-1", ["angry", "sad"]);
        job.Moods[0].Status = NpcMoodStatus.Failed;
        job.Moods[0].Error = "generation failed";
        job.Moods[1].Status = NpcMoodStatus.Ready;
        _jobs.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoiceJob, bool>>())).Returns(job);
        _jobs.Setup(x => x.Replace(It.IsAny<DomainNpcVoiceJob>())).Returns(Task.CompletedTask);

        var result = await _sut.GenerateMoods("smuggler", new GenerateMoodsRequest { Moods = ["Angry", "happy"] });

        result.Moods.Should().HaveCount(3);
        result.Moods.Single(m => m.Mood == "angry").Should().Match<NpcMoodTask>(m => m.Status == NpcMoodStatus.Pending && m.Error == null);
        result.Moods.Single(m => m.Mood == "sad").Status.Should().Be(NpcMoodStatus.Ready);
        result.Moods.Single(m => m.Mood == "happy").Status.Should().Be(NpcMoodStatus.Pending);
    }

    [Fact]
    public async Task GenerateMoods_rejects_an_unknown_mood()
    {
        GivenVoices(Parent);
        var act = () => _sut.GenerateMoods("smuggler", new GenerateMoodsRequest { Moods = ["furious"] });
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GenerateMoods_when_every_mood_exists_asks_for_a_choice()
    {
        GivenVoices([Parent, .. MoodScripts.Generated.Select(m => new DomainNpcVoice { VoiceId = $"smuggler_{m}", MoodOf = "smuggler" })]);
        var act = () => _sut.GenerateMoods("smuggler");
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*every mood*");
    }
}
