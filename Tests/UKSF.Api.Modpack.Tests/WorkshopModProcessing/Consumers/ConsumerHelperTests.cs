using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing.Consumers;

public class ConsumerHelperTests
{
    private readonly Mock<ConsumeContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModsContext> _mockWorkshopModsContext = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private WorkshopModOperationFaulted _publishedFault;

    public ConsumerHelperTests()
    {
        _mockContext.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
                    .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => _publishedFault = msg)
                    .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RunOperationStep_WhenOperationSucceeds_ShouldCallOnSuccessAndNotPublishFault()
    {
        var result = OperationResult.Successful();
        OperationResult receivedResult = null;

        await ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Testing",
            () => Task.FromResult(result),
            r =>
            {
                receivedResult = r;
                return Task.CompletedTask;
            },
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        receivedResult.Should().BeSameAs(result);
        _publishedFault.Should().BeNull();
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
        _mockLogger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Never);
        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), It.IsAny<WorkshopModStatus>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunOperationStep_WhenOperationFails_ShouldLogErrorAndUpdateStatusAndPublishFault()
    {
        var mod = new DomainWorkshopMod { SteamId = "mod1" };
        _mockWorkshopModsContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(mod)))).Returns(mod);
        _mockProcessingService.Setup(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "something broke")).Returns(Task.CompletedTask);
        var onSuccessCalled = false;

        await ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Downloading",
            () => Task.FromResult(OperationResult.Failure("something broke")),
            _ =>
            {
                onSuccessCalled = true;
                return Task.CompletedTask;
            },
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        onSuccessCalled.Should().BeFalse();
        _mockLogger.Verify(x => x.LogError("Downloading failed for mod1: something broke"), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "something broke"), Times.Once);
        _publishedFault.Should().NotBeNull();
        _publishedFault!.WorkshopModId.Should().Be("mod1");
        _publishedFault.ErrorMessage.Should().Be("something broke");
        _publishedFault.FaultedState.Should().Be("Downloading");
    }

    [Fact]
    public async Task RunOperationStep_WhenOperationCancelled_ShouldLogWarningAndUpdateStatusAndPublishFault()
    {
        var mod = new DomainWorkshopMod { SteamId = "mod1" };
        _mockWorkshopModsContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(mod)))).Returns(mod);
        _mockProcessingService.Setup(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "Operation cancelled")).Returns(Task.CompletedTask);

        await ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Checking",
            () => throw new OperationCanceledException(),
            _ => Task.CompletedTask,
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        _mockLogger.Verify(x => x.LogWarning("Checking cancelled for mod1"), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "Operation cancelled"), Times.Once);
        _publishedFault.Should().NotBeNull();
        _publishedFault!.WorkshopModId.Should().Be("mod1");
        _publishedFault.ErrorMessage.Should().Be("Operation cancelled");
        _publishedFault.FaultedState.Should().Be("Checking");
    }

    [Fact]
    public async Task RunOperationStep_WhenUnexpectedExceptionThrown_ShouldLogErrorWithExceptionAndUpdateStatusAndPublishFault()
    {
        var exception = new InvalidOperationException("boom");
        var mod = new DomainWorkshopMod { SteamId = "mod1" };
        _mockWorkshopModsContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(mod)))).Returns(mod);
        _mockProcessingService.Setup(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "boom")).Returns(Task.CompletedTask);

        await ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Executing",
            () => throw exception,
            _ => Task.CompletedTask,
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        _mockLogger.Verify(x => x.LogError("Unexpected error during Executing for mod1", exception), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(mod, WorkshopModStatus.Error, "boom"), Times.Once);
        _publishedFault.Should().NotBeNull();
        _publishedFault!.WorkshopModId.Should().Be("mod1");
        _publishedFault.ErrorMessage.Should().Be("boom");
        _publishedFault.FaultedState.Should().Be("Executing");
    }

    [Fact]
    public async Task RunOperationStep_WhenFailureAndModNotFound_ShouldPublishFaultWithoutUpdatingStatus()
    {
        _mockWorkshopModsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Downloading",
            () => Task.FromResult(OperationResult.Failure("error")),
            _ => Task.CompletedTask,
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), It.IsAny<WorkshopModStatus>(), It.IsAny<string>()), Times.Never);
        _publishedFault.Should().NotBeNull();
        _publishedFault!.WorkshopModId.Should().Be("mod1");
        _publishedFault.ErrorMessage.Should().Be("error");
        _publishedFault.FaultedState.Should().Be("Downloading");
    }

    [Fact]
    public async Task RunOperationStep_WhenOnSuccessThrows_ShouldNotCatchException()
    {
        var result = OperationResult.Successful();
        var expectedException = new InvalidOperationException("onSuccess failed");

        var act = () => ConsumerHelper.RunOperationStep(
            _mockContext.Object,
            "mod1",
            "Testing",
            () => Task.FromResult(result),
            _ => throw expectedException,
            _mockProcessingService.Object,
            _mockWorkshopModsContext.Object,
            _mockLogger.Object
        );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("onSuccess failed");
        _publishedFault.Should().BeNull();
    }
}
