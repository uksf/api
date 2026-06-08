using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class DomainNpcVoiceJobTests
{
    [Fact]
    public void NewJob_factory_seeds_the_four_generated_moods_as_pending()
    {
        var job = DomainNpcVoiceJob.NewJob("smuggler", "owner1");

        job.BaseVoiceId.Should().Be("smuggler");
        job.OwnerId.Should().Be("owner1");
        job.Moods.Select(m => m.Mood).Should().BeEquivalentTo(MoodScripts.Generated);
        job.Moods.Should().OnlyContain(m => m.Status == NpcMoodStatus.Pending);
    }
}
