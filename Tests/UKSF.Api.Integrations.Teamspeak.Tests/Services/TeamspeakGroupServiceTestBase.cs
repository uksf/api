using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Query;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public abstract class TeamspeakGroupServiceTestBase
{
    private static readonly DomainVariableItem TeamspeakGidUnverified = new() { Key = "TEAMSPEAK_GID_UNVERIFIED", Item = "1" };
    private static readonly DomainVariableItem TeamspeakGidDischarged = new() { Key = "TEAMSPEAK_GID_DISCHARGED", Item = "2" };
    private static readonly DomainVariableItem TeamspeakGidRoot = new() { Key = "TEAMSPEAK_GID_ROOT", Item = "3" };
    private static readonly DomainVariableItem TeamspeakGidElcom = new() { Key = "TEAMSPEAK_GID_ELCOM", Item = "4" };
    private static readonly DomainVariableItem TeamspeakGidBlacklist = new() { Key = "TEAMSPEAK_GID_BLACKLIST", Item = "99,100" };

    protected readonly List<int> _addedGroups = [];

    protected readonly DomainUnit _elcomUnit = new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "ELCOM",
        Branch = UnitBranch.Auxiliary,
        Parent = ObjectId.Empty.ToString()
    };

    protected readonly Mock<IAccountContext> _mockAccountContext = new();
    protected readonly Mock<IRanksContext> _mockRanksContext = new();
    protected readonly Mock<ITeamspeakQuery> _mockTeamspeakQuery = new();
    protected readonly Mock<ITrainingsContext> _mockTrainingsContext = new();
    protected readonly Mock<IUnitsContext> _mockUnitsContext = new();
    protected readonly Mock<IVariablesService> _mockVariablesService = new();
    protected readonly Mock<IUksfLogger> _mockLogger = new();
    protected readonly List<int> _removedGroups = [];
    protected readonly TeamspeakGroupService _teamspeakGroupService;

    protected TeamspeakGroupServiceTestBase()
    {
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_UNVERIFIED")).Returns(TeamspeakGidUnverified);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_DISCHARGED")).Returns(TeamspeakGidDischarged);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ROOT")).Returns(TeamspeakGidRoot);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ELCOM")).Returns(TeamspeakGidElcom);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_BLACKLIST")).Returns(TeamspeakGidBlacklist);

        _mockTeamspeakQuery.Setup(x => x.AddServerGroup(It.IsAny<int>(), It.IsAny<int>()))
                           .Returns(Task.CompletedTask)
                           .Callback((int _, int serverGroup) => _addedGroups.Add(serverGroup));
        _mockTeamspeakQuery.Setup(x => x.RemoveServerGroup(It.IsAny<int>(), It.IsAny<int>()))
                           .Returns(Task.CompletedTask)
                           .Callback((int _, int serverGroup) => _removedGroups.Add(serverGroup));

        var unitsService = new UnitsService(
            _mockUnitsContext.Object,
            new Mock<IRanksService>().Object,
            new Mock<IChainOfCommandService>().Object,
            new Mock<IDisplayNameService>().Object,
            new Mock<IAccountContext>().Object,
            new UnitMapper(),
            new Mock<IMedicAttachmentService>().Object,
            new Mock<IVariablesContext>().Object
        );
        _teamspeakGroupService = CreateService(unitsService);
    }

    protected TeamspeakGroupService CreateService(IUnitsService unitsService)
    {
        return new TeamspeakGroupService(
            _mockAccountContext.Object,
            _mockRanksContext.Object,
            _mockUnitsContext.Object,
            unitsService,
            _mockTeamspeakQuery.Object,
            _mockVariablesService.Object,
            _mockTrainingsContext.Object,
            _mockLogger.Object
        );
    }

    protected void SetupUnitsContext(List<DomainUnit> units)
    {
        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
    }

    protected void SetupRank(string rank, string teamspeakGroup)
    {
        _mockRanksContext.Setup(x => x.GetSingle(rank)).Returns(new DomainRank { Name = rank, TeamspeakGroup = teamspeakGroup });
    }

    protected static DomainAccount CreateMember(string id, string rank = "Private", string unitAssignment = null)
    {
        return new DomainAccount
        {
            Id = id,
            MembershipState = MembershipState.Member,
            Rank = rank,
            UnitAssignment = unitAssignment
        };
    }
}
