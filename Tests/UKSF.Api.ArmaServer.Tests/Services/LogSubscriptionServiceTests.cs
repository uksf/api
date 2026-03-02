using System;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class LogSubscriptionServiceTests : IDisposable
{
    private readonly LogSubscriptionService _sut = new();

    public void Dispose()
    {
        _sut.Dispose();
    }

    #region Subscription tracking

    [Fact]
    public void AddAndRemoveAll_TracksGroupsForConnection()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");

        var result = _sut.RemoveAllSubscriptions("conn-1");

        result.Should().ContainSingle().Which.Should().Be("log:server1:Server");
    }

    [Fact]
    public void AddSubscription_MultipleGroups_TracksAll()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");
        _sut.AddSubscription("conn-1", "log:server1:Jarvis");

        var result = _sut.RemoveAllSubscriptions("conn-1");

        result.Should().HaveCount(2);
        result.Should().Contain("log:server1:Server");
        result.Should().Contain("log:server1:Jarvis");
    }

    [Fact]
    public void AddSubscription_DuplicateGroup_IsIdempotent()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");
        _sut.AddSubscription("conn-1", "log:server1:Server");

        var result = _sut.RemoveAllSubscriptions("conn-1");

        result.Should().ContainSingle();
    }

    [Fact]
    public void RemoveSubscription_RemovesGroupFromConnection()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");
        _sut.AddSubscription("conn-1", "log:server1:Jarvis");

        _sut.RemoveSubscription("conn-1", "log:server1:Server");

        var result = _sut.RemoveAllSubscriptions("conn-1");
        result.Should().ContainSingle().Which.Should().Be("log:server1:Jarvis");
    }

    [Fact]
    public void RemoveSubscription_UnknownConnection_DoesNotThrow()
    {
        var action = () => _sut.RemoveSubscription("unknown", "log:server1:Server");

        action.Should().NotThrow();
    }

    [Fact]
    public void RemoveAllSubscriptions_ClearsConnection()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");

        _sut.RemoveAllSubscriptions("conn-1");

        var secondResult = _sut.RemoveAllSubscriptions("conn-1");
        secondResult.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAllSubscriptions_UnknownConnection_ReturnsEmpty()
    {
        var result = _sut.RemoveAllSubscriptions("unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public void MultipleConnections_TrackedIndependently()
    {
        _sut.AddSubscription("conn-1", "log:server1:Server");
        _sut.AddSubscription("conn-2", "log:server1:Jarvis");

        var result1 = _sut.RemoveAllSubscriptions("conn-1");
        var result2 = _sut.RemoveAllSubscriptions("conn-2");

        result1.Should().ContainSingle().Which.Should().Be("log:server1:Server");
        result2.Should().ContainSingle().Which.Should().Be("log:server1:Jarvis");
    }

    #endregion

    #region Watcher management

    [Fact]
    public void StartOrJoinWatcher_CreatesWatcherViaFactory()
    {
        var factoryCalled = false;
        var mockWatcher = new Mock<IDisposable>();

        _sut.StartOrJoinWatcher(
            "log:server1:Server",
            () =>
            {
                factoryCalled = true;
                return mockWatcher.Object;
            }
        );

        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public void StartOrJoinWatcher_SecondCall_DoesNotCallFactoryAgain()
    {
        var callCount = 0;
        var mockWatcher = new Mock<IDisposable>();

        _sut.StartOrJoinWatcher(
            "log:server1:Server",
            () =>
            {
                callCount++;
                return mockWatcher.Object;
            }
        );
        _sut.StartOrJoinWatcher(
            "log:server1:Server",
            () =>
            {
                callCount++;
                return mockWatcher.Object;
            }
        );

        callCount.Should().Be(1, "factory should only be called once for the same group");
    }

    [Fact]
    public void StopOrLeaveWatcher_WithSingleRef_DisposesWatcher()
    {
        var mockWatcher = new Mock<IDisposable>();
        _sut.StartOrJoinWatcher("log:server1:Server", () => mockWatcher.Object);

        _sut.StopOrLeaveWatcher("log:server1:Server");

        mockWatcher.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void StopOrLeaveWatcher_WithMultipleRefs_DoesNotDisposeUntilAllLeave()
    {
        var mockWatcher = new Mock<IDisposable>();
        _sut.StartOrJoinWatcher("log:server1:Server", () => mockWatcher.Object);
        _sut.StartOrJoinWatcher("log:server1:Server", () => mockWatcher.Object);

        _sut.StopOrLeaveWatcher("log:server1:Server");
        mockWatcher.Verify(x => x.Dispose(), Times.Never);

        _sut.StopOrLeaveWatcher("log:server1:Server");
        mockWatcher.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void StopOrLeaveWatcher_AfterDisposal_CanRecreateWatcher()
    {
        var watcher1 = new Mock<IDisposable>();
        var watcher2 = new Mock<IDisposable>();

        _sut.StartOrJoinWatcher("log:server1:Server", () => watcher1.Object);
        _sut.StopOrLeaveWatcher("log:server1:Server");
        watcher1.Verify(x => x.Dispose(), Times.Once);

        _sut.StartOrJoinWatcher("log:server1:Server", () => watcher2.Object);
        _sut.StopOrLeaveWatcher("log:server1:Server");
        watcher2.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void StopOrLeaveWatcher_UnknownGroup_DoesNotThrow()
    {
        var action = () => _sut.StopOrLeaveWatcher("unknown-group");

        action.Should().NotThrow();
    }

    [Fact]
    public void MultipleGroups_TrackedIndependently()
    {
        var watcher1 = new Mock<IDisposable>();
        var watcher2 = new Mock<IDisposable>();

        _sut.StartOrJoinWatcher("log:server1:Server", () => watcher1.Object);
        _sut.StartOrJoinWatcher("log:server1:Jarvis", () => watcher2.Object);

        _sut.StopOrLeaveWatcher("log:server1:Server");

        watcher1.Verify(x => x.Dispose(), Times.Once);
        watcher2.Verify(x => x.Dispose(), Times.Never);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DisposesAllActiveWatchers()
    {
        var watcher1 = new Mock<IDisposable>();
        var watcher2 = new Mock<IDisposable>();

        _sut.StartOrJoinWatcher("log:server1:Server", () => watcher1.Object);
        _sut.StartOrJoinWatcher("log:server1:Jarvis", () => watcher2.Object);

        _sut.Dispose();

        watcher1.Verify(x => x.Dispose(), Times.Once);
        watcher2.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var watcher = new Mock<IDisposable>();
        _sut.StartOrJoinWatcher("log:server1:Server", () => watcher.Object);

        _sut.Dispose();
        _sut.Dispose();

        watcher.Verify(x => x.Dispose(), Times.Once);
    }

    #endregion
}
