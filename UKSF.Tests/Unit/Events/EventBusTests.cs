using System;
using FluentAssertions;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Events {
    public class EventBusTests {
        [Fact]
        public void Should_return_observable() {
            EventBus<DataEventModel<TestDataModel>> eventBus = new EventBus<DataEventModel<TestDataModel>>();

            IObservable<DataEventModel<TestDataModel>> subject = eventBus.AsObservable();

            subject.Should().NotBeNull();
            subject.Should().BeAssignableTo<IObservable<DataEventModel<TestDataModel>>>();
        }
    }
}
