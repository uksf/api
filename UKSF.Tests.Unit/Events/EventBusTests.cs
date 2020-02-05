using System;
using FluentAssertions;
using UKSF.Api.Events;
using UKSF.Api.Models.Events;
using UKSF.Tests.Unit.Data;
using Xunit;

namespace UKSF.Tests.Unit.Events {
    public class EventBusTests {
        [Fact]
        public void ShouldReturnObservable() {
            EventBus<DataEventModel<IMockDataService>> eventBus = new EventBus<DataEventModel<IMockDataService>>();

            IObservable<DataEventModel<IMockDataService>> subject = eventBus.AsObservable();

            subject.Should().NotBeNull();
        }
    }
}
