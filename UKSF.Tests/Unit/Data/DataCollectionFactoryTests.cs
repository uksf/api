using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Tests.Unit.Data;

public class DataCollectionFactoryTests
{
    [Fact]
    public void ShouldCreateDataCollection()
    {
        Mock<IMongoDatabase> mockMongoDatabase = new();

        MongoCollectionFactory mongoCollectionFactory = new(mockMongoDatabase.Object);

        var subject = mongoCollectionFactory.CreateMongoCollection<DomainTestModel>("test");

        subject.Should().NotBeNull();
    }
}
