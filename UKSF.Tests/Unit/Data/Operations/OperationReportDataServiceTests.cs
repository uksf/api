﻿using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Data.Operations;

public class OperationReportDataServiceTests
{
    private readonly Mock<IMongoCollection<Oprep>> _mockDataCollection;
    private readonly OperationReportContext _operationReportContext;

    public OperationReportDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Oprep>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _operationReportContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
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
