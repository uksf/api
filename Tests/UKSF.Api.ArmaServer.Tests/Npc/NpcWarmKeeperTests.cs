using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public class NpcWarmKeeperTests
{
    private static (NpcWarmKeeper keeper, Mock<IClacksClient> clacks) Build(IEnumerable<DomainNpcSession> sessions)
    {
        var sessionsContext = new Mock<INpcSessionsContext>();
        sessionsContext.Setup(x => x.Get()).Returns(sessions.ToList());
        var clacks = new Mock<IClacksClient>();
        clacks.Setup(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>())).ReturnsAsync(true);
        var keeper = new NpcWarmKeeper(sessionsContext.Object, clacks.Object, new Mock<IUksfLogger>().Object);
        return (keeper, clacks);
    }

    [Fact]
    public async Task Tick_WithLiveSessions_WarmsNpcAndVoiceRoles()
    {
        var (keeper, clacks) = Build([new DomainNpcSession { NpcId = "npc1", SessionId = "s1" }]);

        await keeper.TickAsync();

        clacks.Verify(
            x => x.WarmAsync(It.Is<IReadOnlyCollection<string>>(r => r.Contains("npc") && r.Contains("npc-voice")), NpcWarmKeeper.LeaseMs),
            Times.Once
        );
    }

    [Fact]
    public async Task Tick_WithNoSessions_DoesNotWarm()
    {
        var (keeper, clacks) = Build([]);

        await keeper.TickAsync();

        clacks.Verify(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>()), Times.Never);
    }
}
