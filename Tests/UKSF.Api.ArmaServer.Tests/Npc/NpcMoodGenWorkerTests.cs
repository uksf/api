using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcMoodGenWorkerTests
{
    private static (NpcMoodGenWorker worker, Mock<INpcVoiceJobsContext> jobs, Mock<INpcVoicesContext> voices, Mock<INpcVoiceStore> store, Mock<IClacksClient>
        clacks) Build(DomainNpcVoiceJob job)
    {
        var jobs = new Mock<INpcVoiceJobsContext>();
        jobs.Setup(x => x.Get()).Returns(new List<DomainNpcVoiceJob> { job });
        var voices = new Mock<INpcVoicesContext>();
        voices.Setup(x => x.GetSingle(It.IsAny<Func<DomainNpcVoice, bool>>()))
              .Returns((Func<DomainNpcVoice, bool> p) => p(new DomainNpcVoice { VoiceId = "smuggler", DisplayName = "Smuggler" })
                           ? new DomainNpcVoice { VoiceId = "smuggler", DisplayName = "Smuggler" }
                           : null
              );
        var store = new Mock<INpcVoiceStore>();
        store.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync((string id, byte[] _) => $"{id}.wav");
        var clacks = new Mock<IClacksClient>();
        var worker = new NpcMoodGenWorker(jobs.Object, voices.Object, store.Object, clacks.Object, new Mock<IUksfLogger>().Object);
        return (worker, jobs, voices, store, clacks);
    }

    [Fact]
    public async Task Ok_emote_stores_registers_pushes_and_marks_ready()
    {
        var job = DomainNpcVoiceJob.NewJob("smuggler", "owner1");
        var (worker, jobs, voices, store, clacks) = Build(job);
        clacks.Setup(x => x.EmoteAsync("smuggler", It.IsAny<string>(), It.IsAny<string>(), MoodScripts.EmoAlpha))
              .ReturnsAsync(
                  new ClacksEmoteResult
                  {
                      Status = EmoteStatus.Ok,
                      WavBytes = [1, 2, 3],
                      DurationMs = 4000
                  }
              );
        clacks.Setup(x => x.PutVoiceAsync(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync(true);

        await worker.DrainOnceAsync();

        job.Moods.Should().OnlyContain(m => m.Status == NpcMoodStatus.Ready);
        store.Verify(x => x.SaveAsync("smuggler_angry", It.IsAny<byte[]>()), Times.Once);
        voices.Verify(x => x.Add(It.Is<DomainNpcVoice>(v => v.VoiceId == "smuggler_angry" && v.MoodOf == "smuggler")), Times.Once);
        clacks.Verify(x => x.PutVoiceAsync("smuggler_angry", It.IsAny<byte[]>()), Times.Once);
        jobs.Verify(x => x.Replace(It.IsAny<DomainNpcVoiceJob>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task NodeDown_leaves_moods_pending_for_retry()
    {
        var job = DomainNpcVoiceJob.NewJob("smuggler", "owner1");
        var (worker, _, _, store, clacks) = Build(job);
        clacks.Setup(x => x.EmoteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>()))
              .ReturnsAsync(ClacksEmoteResult.NodeDown());

        await worker.DrainOnceAsync();

        job.Moods.Should().OnlyContain(m => m.Status == NpcMoodStatus.Pending);
        store.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task Failed_emote_marks_only_that_mood_failed_and_continues()
    {
        var job = DomainNpcVoiceJob.NewJob("smuggler", "owner1");
        var (worker, _, _, _, clacks) = Build(job);
        clacks.Setup(x => x.PutVoiceAsync(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync(true);
        clacks.Setup(x => x.EmoteAsync("smuggler", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>()))
              .ReturnsAsync((string _, string _, string emoText, double _) => emoText.Contains("furious")
                                ? ClacksEmoteResult.Failed()
                                : new ClacksEmoteResult
                                {
                                    Status = EmoteStatus.Ok,
                                    WavBytes = [1],
                                    DurationMs = 3000
                                }
              );

        await worker.DrainOnceAsync();

        job.Moods.Single(m => m.Mood == "angry").Status.Should().Be(NpcMoodStatus.Failed);
        job.Moods.Where(m => m.Mood != "angry").Should().OnlyContain(m => m.Status == NpcMoodStatus.Ready);
    }
}
