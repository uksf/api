using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServersServiceOrderTests
{
    private readonly Mock<IGameServerHelpers> _mockIGameServerHelpers = new();
    private readonly Mock<IGameServersContext> _mockIGameServersContext = new();
    private readonly Mock<IMissionPatchingService> _mockIMissionPatchingService = new();
    private readonly Mock<IVariablesService> _mockIVariablesService = new();

    private readonly GameServersService _subject;

    public GameServersServiceOrderTests()
    {
        _subject = new GameServersService(
            _mockIGameServersContext.Object,
            _mockIMissionPatchingService.Object,
            _mockIGameServerHelpers.Object,
            _mockIVariablesService.Object
        );
    }

    public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[]
            {
                3,
                5,
                new List<DomainGameServer>
                {
                    new() { Id = "A", Order = 0 },
                    new() { Id = "B", Order = 1 },
                    new() { Id = "C", Order = 2 },
                    new() { Id = "E", Order = 3 },
                    new() { Id = "F", Order = 4 },
                    new() { Id = "D", Order = 5 },
                    new() { Id = "G", Order = 6 }
                }
            },
            new object[]
            {
                0,
                3,
                new List<DomainGameServer>
                {
                    new() { Id = "B", Order = 0 },
                    new() { Id = "C", Order = 1 },
                    new() { Id = "D", Order = 2 },
                    new() { Id = "A", Order = 3 },
                    new() { Id = "E", Order = 4 },
                    new() { Id = "F", Order = 5 },
                    new() { Id = "G", Order = 6 }
                }
            },
            new object[]
            {
                0,
                6,
                new List<DomainGameServer>
                {
                    new() { Id = "B", Order = 0 },
                    new() { Id = "C", Order = 1 },
                    new() { Id = "D", Order = 2 },
                    new() { Id = "E", Order = 3 },
                    new() { Id = "F", Order = 4 },
                    new() { Id = "G", Order = 5 },
                    new() { Id = "A", Order = 6 }
                }
            },
            new object[]
            {
                6,
                0,
                new List<DomainGameServer>
                {
                    new() { Id = "G", Order = 0 },
                    new() { Id = "A", Order = 1 },
                    new() { Id = "B", Order = 2 },
                    new() { Id = "C", Order = 3 },
                    new() { Id = "D", Order = 4 },
                    new() { Id = "E", Order = 5 },
                    new() { Id = "F", Order = 6 }
                }
            },
            new object[]
            {
                5,
                3,
                new List<DomainGameServer>
                {
                    new() { Id = "A", Order = 0 },
                    new() { Id = "B", Order = 1 },
                    new() { Id = "C", Order = 2 },
                    new() { Id = "F", Order = 3 },
                    new() { Id = "D", Order = 4 },
                    new() { Id = "E", Order = 5 },
                    new() { Id = "G", Order = 6 }
                }
            }
        };

    [Theory]
    [MemberData(nameof(Data))]
    public async Task When_setting_server_order(int previousIndex, int newIndex, List<DomainGameServer> expected)
    {
        var gameServers = Given_game_servers();
        When_updating_game_server_order(gameServers);

        await _subject.UpdateGameServerOrder(new OrderUpdateRequest { PreviousIndex = previousIndex, NewIndex = newIndex });

        gameServers.Should().BeEquivalentTo(expected);
    }

    private List<DomainGameServer> Given_game_servers()
    {
        var gameServers = new List<DomainGameServer>
        {
            new() { Id = "A", Order = 0 },
            new() { Id = "B", Order = 1 },
            new() { Id = "C", Order = 2 },
            new() { Id = "D", Order = 3 },
            new() { Id = "E", Order = 4 },
            new() { Id = "F", Order = 5 },
            new() { Id = "G", Order = 6 }
        };
        _mockIGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        return gameServers;
    }

    private void When_updating_game_server_order(IReadOnlyCollection<DomainGameServer> gameServers)
    {
        _mockIGameServersContext.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainGameServer, int>>>(), It.IsAny<int>()))
                                .Callback((string id, Expression<Func<DomainGameServer, int>> _, int index) =>
                                          {
                                              gameServers.First(x => x.Id == id).Order = index;
                                          }
                                );
    }
}
