using System;
using FluentAssertions;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events;

public class EventBusTests
{
    [Fact]
    public void When_getting_event_bus_observable()
    {
        EventBus eventBus = new();

        var subject = eventBus.AsObservable();

        subject.Should().NotBeNull();
        subject.Should().BeAssignableTo<IObservable<EventModel>>();
    }
}
