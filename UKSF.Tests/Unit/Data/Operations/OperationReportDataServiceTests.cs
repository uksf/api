using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations
{
    public class OperationReportDataServiceTests
    {
        private readonly Mock<IMongoCollection<Oprep>> _mockDataCollection;
        private readonly OperationReportContext _operationReportContext;

        public OperationReportDataServiceTests()
        {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            _mockDataCollection = new();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Oprep>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

            _operationReportContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order()
        {
            Oprep item1 = new() { Start = DateTime.UtcNow.AddDays(-1) };
            Oprep item2 = new() { Start = DateTime.UtcNow.AddDays(-2) };
            Oprep item3 = new() { Start = DateTime.UtcNow.AddDays(-3) };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> { item1, item2, item3 });

            var subject = _operationReportContext.Get();

            subject.Should().ContainInOrder(item3, item2, item1);
        }

        [Fact]
        public void ShouldGetOrderedCollectionByPredicate()
        {
            Oprep item1 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-1) };
            Oprep item2 = new() { Description = "2", Start = DateTime.UtcNow.AddDays(-2) };
            Oprep item3 = new() { Description = "1", Start = DateTime.UtcNow.AddDays(-3) };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Oprep> { item1, item2, item3 });

            var subject = _operationReportContext.Get(x => x.Description == "1");

            subject.Should().ContainInOrder(item3, item1);
        }
    }
}
