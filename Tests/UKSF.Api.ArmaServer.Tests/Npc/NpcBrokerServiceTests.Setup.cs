using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc;

public partial class NpcBrokerServiceTests
{
    private readonly Mock<INpcSessionsContext> _sessionsContext = new();
    private readonly Mock<INpcAudioClipsContext> _clipsContext = new();
    private readonly Mock<INpcBrainClient> _brainClient = new();
    private readonly Mock<IClacksClient> _clacks = new();
    private readonly Mock<IGameServerCommandSender> _commandSender = new();
    private readonly Mock<INpcAudioStore> _audioStore = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<INpcVoicesContext> _voicesContext = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly NpcBrokerService _sut;

    // Mirrors the broker's filler set; asserting against it keeps the tests honest when
    // the set grows without pinning them to a count.
    private static readonly IReadOnlyList<string> FillerIds = ["s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "l0", "l1", "l2", "l3", "l4"];

    public NpcBrokerServiceTests()
    {
        _variablesService.Setup(x => x.GetFeatureState("NPC_BROKER")).Returns(true);
        _sessionsContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcSession, bool>>())).Returns((DomainNpcSession)null);
        _clipsContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainNpcAudioClip, bool>>())).Returns((DomainNpcAudioClip)null);
        _sessionsContext.Setup(x => x.Add(It.IsAny<DomainNpcSession>())).Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.Replace(It.IsAny<DomainNpcSession>())).Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.Update(It.IsAny<Expression<Func<DomainNpcSession, bool>>>(), It.IsAny<UpdateDefinition<DomainNpcSession>>()))
                        .Returns(Task.CompletedTask);
        _sessionsContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcSession, bool>>>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.Add(It.IsAny<DomainNpcAudioClip>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.Replace(It.IsAny<DomainNpcAudioClip>())).Returns(Task.CompletedTask);
        _clipsContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainNpcAudioClip, bool>>>())).Returns(Task.CompletedTask);
        _commandSender.Setup(x => x.SendCommandAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _audioStore.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                   .ReturnsAsync((string sessionId, string npcId, string clipId, byte[] _) => $"2026-06-07/{sessionId}_{npcId}_{clipId}.wav");
        _audioStore.Setup(x => x.ReadAsync(It.IsAny<string>())).ReturnsAsync(Convert.FromBase64String("QUJD"));

        _brainClient.Setup(x => x.PrerenderAsync(It.IsAny<PrerenderRequest>()))
                    .ReturnsAsync(
                        new PrerenderResult
                        {
                            Items = FillerIds.Select(id => new PrerenderResultItem
                                                 {
                                                     Id = id,
                                                     AudioBase64 = "QQ==",
                                                     DurationMs = 100
                                                 }
                                             )
                                             .ToList()
                        }
                    );

        _clacks.Setup(x => x.WarmAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>())).ReturnsAsync(true);

        _sut = new NpcBrokerService(
            _sessionsContext.Object,
            _clipsContext.Object,
            _brainClient.Object,
            _clacks.Object,
            _commandSender.Object,
            _audioStore.Object,
            _voicesContext.Object,
            _variablesService.Object,
            _logger.Object
        );
    }
}
