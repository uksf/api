using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerEventHandlerNpcTraceTests
{
    [Theory]
    [InlineData("npc_utterance")]
    [InlineData("npc_ack")]
    public async Task HandleEventAsync_NpcTraceEvents_RouteToBroker(string type)
    {
        var broker = new Mock<INpcBrokerService>();
        var sut = new GameServerEventHandler(
            Mock.Of<IGameServerProcessManager>(),
            Mock.Of<IGameServersContext>(),
            Mock.Of<IPublishEndpoint>(),
            Mock.Of<IMissionStatsService>(),
            Mock.Of<IPerformanceService>(),
            Mock.Of<IPersistenceSessionsService>(),
            Mock.Of<IUksfLogger>(),
            broker.Object,
            Mock.Of<IMissionSessionCaptureService>()
        );
        var data = new Dictionary<string, object> { { "sessionId", "s1" } };

        await sut.HandleEventAsync(
            new GameServerEvent
            {
                Type = type,
                ApiPort = 5006,
                Data = data
            }
        );

        broker.Verify(x => x.HandleTraceEventAsync(type, data), Times.Once);
    }
}
