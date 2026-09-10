using System;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Tests.Integration;

internal sealed class CampaignHierarchyMongoHarness : IDisposable
{
    private readonly MongoDbRunner _runner;

    public CampaignHierarchyMongoHarness()
    {
        _runner = MongoDbRunner.Start(singleNodeReplSet: false);
        Database = new MongoClient(_runner.ConnectionString).GetDatabase($"hierarchy-{ObjectId.GenerateNewId()}");
        MongoCollectionFactory factory = new(Database);
        EventBus eventBus = new();

        Mock<IVariablesService> variables = new();
        variables.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        Campaigns = new CampaignsContext(factory, eventBus, variables.Object);
        Operations = new OperationsContext(factory, eventBus, variables.Object);
        Missions = new CampaignMissionsContext(factory, eventBus, variables.Object);
        IntelPages = new IntelPagesContext(factory, eventBus, variables.Object);

        Http.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        Http.Setup(x => x.GetUserId()).Returns(ObjectId.GenerateNewId().ToString());

        var serverId = ObjectId.GenerateNewId().ToString();
        GameServers.Setup(x => x.GetServers()).Returns([new DomainGameServer { Id = serverId, Name = "Main Server" }]);
        GameServers.Setup(x => x.GetServer(It.IsAny<string>())).Returns(new DomainGameServer { Id = serverId, Name = "Main Server" });
        FileMissions.Setup(x => x.FindMissionFilePath("m.Altis.pbo")).Returns("/tmp/m.Altis.pbo");
        Launch.Setup(x => x.LaunchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync([]);

        CampaignMissionsService = new CampaignMissionsService(
            GameServers.Object,
            FileMissions.Object,
            Missions,
            IntelPages,
            Launch.Object
        );
        OperationsService = new OperationsService(Operations, Missions, CampaignMissionsService, IntelPages);

        CampaignsController = new CampaignsController(Campaigns, Operations, IntelPages, OperationsService, Http.Object, Logger.Object);
        OperationsController = new OperationsController(Operations, Campaigns, OperationsService, Http.Object, Logger.Object);
        CampaignMissionsController = new CampaignMissionsController(
            Missions,
            Operations,
            Campaigns,
            CampaignMissionsService,
            GameServers.Object,
            Http.Object,
            Logger.Object
        );
        IntelPagesController = new IntelPagesController(IntelPages, Campaigns, Operations, Missions, Http.Object, Logger.Object);
    }

    public IMongoDatabase Database { get; }
    public CampaignsContext Campaigns { get; }
    public OperationsContext Operations { get; }
    public CampaignMissionsContext Missions { get; }
    public IntelPagesContext IntelPages { get; }
    public CampaignMissionsService CampaignMissionsService { get; }
    public OperationsService OperationsService { get; }
    public CampaignsController CampaignsController { get; }
    public OperationsController OperationsController { get; }
    public CampaignMissionsController CampaignMissionsController { get; }
    public IntelPagesController IntelPagesController { get; }
    public Mock<IHttpContextService> Http { get; } = new();
    public Mock<IMissionsService> FileMissions { get; } = new();
    public Mock<IGameServersService> GameServers { get; } = new();
    public Mock<IGameServerLaunchService> Launch { get; } = new();
    public Mock<IUksfLogger> Logger { get; } = new();

    public static string NewId() => ObjectId.GenerateNewId().ToString();

    public void Dispose()
    {
        _runner.Dispose();
        GC.SuppressFinalize(this);
    }
}
