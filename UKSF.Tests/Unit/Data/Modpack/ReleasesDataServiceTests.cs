﻿using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Modpack;

public class ReleasesDataServiceTests
{
    private readonly Mock<IMongoCollection<ModpackRelease>> _mockDataCollection;
    private readonly ReleasesContext _releasesContext;

    public ReleasesDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<ModpackRelease>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _releasesContext = new(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_get_collection_in_order()
    {
        ModpackRelease item1 = new() { Version = "4.19.11" };
        ModpackRelease item2 = new() { Version = "5.19.6" };
        ModpackRelease item3 = new() { Version = "5.18.8" };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<ModpackRelease> { item1, item2, item3 });

        var subject = _releasesContext.Get();

        subject.Should().ContainInOrder(item2, item3, item1);
    }
}
