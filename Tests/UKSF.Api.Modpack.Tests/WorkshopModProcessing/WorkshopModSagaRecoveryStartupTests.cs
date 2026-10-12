using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.WorkshopModProcessing;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class WorkshopModSagaRecoveryStartupTests
{
    private readonly Mock<IQuerySagaRepository<WorkshopModInstanceState>> _query = new();
    private readonly Mock<ILoadSagaRepository<WorkshopModInstanceState>> _load = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly List<object> _published = [];

    public WorkshopModSagaRecoveryStartupTests()
    {
        _bus.Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((message, _) => _published.Add(message))
            .Returns(Task.CompletedTask);
    }

    private void GivenSagas(params WorkshopModInstanceState[] sagas)
    {
        _query.Setup(x => x.Find(It.IsAny<ISagaQuery<WorkshopModInstanceState>>())).ReturnsAsync(sagas.Select(x => x.CorrelationId).ToList());
        foreach (var saga in sagas)
        {
            _load.Setup(x => x.Load(saga.CorrelationId)).ReturnsAsync(saga);
        }
    }

    private static WorkshopModInstanceState Saga(string state, string modId, WorkshopModOperationType? operationType = WorkshopModOperationType.Update) =>
        new()
        {
            CorrelationId = Guid.NewGuid(),
            CurrentState = state,
            WorkshopModId = modId,
            OperationType = operationType
        };

    private Task Start() => new WorkshopModSagaRecoveryStartup(_query.Object, _load.Object, _bus.Object, _logger.Object).StartAsync(CancellationToken.None);

    [Fact]
    public async Task StartAsync_RepublishesDownloadForSagaStuckInDownloading()
    {
        GivenSagas(Saga("Downloading", "3575476126"));

        await Start();

        _published.Should()
                  .ContainSingle()
                  .Which.Should()
                  .BeEquivalentTo(new WorkshopModDownloadCommand { WorkshopModId = "3575476126", OperationType = WorkshopModOperationType.Update });
    }

    [Fact]
    public async Task StartAsync_RepublishesCheckForSagaStuckInChecking()
    {
        GivenSagas(Saga("Checking", "mod1", WorkshopModOperationType.Install));

        await Start();

        _published.Should()
                  .ContainSingle()
                  .Which.Should()
                  .BeEquivalentTo(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
    }

    [Fact]
    public async Task StartAsync_RepublishesExecuteWithSelectedFilesForSagaStuckInExecuting()
    {
        var saga = Saga("Executing", "mod1");
        saga.SelectedPbos = ["a.pbo"];
        saga.SelectedExtensions = ["b.dll"];
        GivenSagas(saga);

        await Start();

        _published.Should()
                  .ContainSingle()
                  .Which.Should()
                  .BeEquivalentTo(
                      new WorkshopModExecuteCommand
                      {
                          WorkshopModId = "mod1",
                          OperationType = WorkshopModOperationType.Update,
                          SelectedPbos = ["a.pbo"],
                          SelectedExtensions = ["b.dll"],
                          Resumed = true
                      }
                  );
    }

    [Fact]
    public async Task StartAsync_RepublishesUninstallForSagaStuckInUninstalling()
    {
        GivenSagas(Saga("Uninstalling", "mod1", null));

        await Start();

        _published.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1", Resumed = true });
    }

    [Fact]
    public async Task StartAsync_RepublishesCleanupWithSagaDataForSagaStuckInCleanup()
    {
        var saga = Saga("Cleanup", "mod1", null);
        saga.FilesChanged = false;
        saga.WorkshopModName = "Mod One";
        GivenSagas(saga);

        await Start();

        _published.Should()
        .ContainSingle()
        .Which.Should()
        .BeEquivalentTo(
            new WorkshopModCleanupCommand
            {
                WorkshopModId = "mod1",
                FilesChanged = false,
                WorkshopModName = "Mod One"
            }
        );
    }

    [Fact]
    public async Task StartAsync_LeavesSagaAwaitingInterventionAlone()
    {
        GivenSagas(Saga("AwaitingIntervention", "waiting"), Saga("Downloading", "mod2"));

        await Start();

        _published.Cast<WorkshopModDownloadCommand>().Select(x => x.WorkshopModId).Should().Equal("mod2");
    }

    [Fact]
    public async Task StartAsync_LogsWhenSagaQueryFails()
    {
        _query.Setup(x => x.Find(It.IsAny<ISagaQuery<WorkshopModInstanceState>>())).ThrowsAsync(new InvalidOperationException("mongo down"));

        await Start();

        _logger.Verify(x => x.LogError(It.Is<Exception>(ex => ex.Message == "mongo down")), Times.Once);
    }

    [Fact]
    public async Task StartAsync_RecoversEverySaga()
    {
        GivenSagas(Saga("Downloading", "3575476126"), Saga("Downloading", "1923321700"));

        await Start();

        _published.Cast<WorkshopModDownloadCommand>().Select(x => x.WorkshopModId).Should().Equal("3575476126", "1923321700");
    }

    [Fact]
    public async Task StartAsync_LogsAndContinuesWhenOneSagaFails()
    {
        var broken = Saga("Downloading", "broken");
        GivenSagas(broken, Saga("Downloading", "mod2"));
        _load.Setup(x => x.Load(broken.CorrelationId)).ThrowsAsync(new InvalidOperationException("boom"));

        await Start();

        _published.Cast<WorkshopModDownloadCommand>().Select(x => x.WorkshopModId).Should().Equal("mod2");
        _logger.Verify(x => x.LogError(It.Is<Exception>(ex => ex.Message == "boom")), Times.Once);
    }
}
