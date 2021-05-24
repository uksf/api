using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel
{
    public class RolesDataServiceTests
    {
        private readonly Mock<IMongoCollection<Role>> _mockDataCollection;
        private readonly RolesContext _rolesContext;

        public RolesDataServiceTests()
        {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<IEventBus> mockEventBus = new();
            _mockDataCollection = new();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Role>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

            _rolesContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
        }

        [Fact]
        public void Should_get_collection_in_order()
        {
            Role role1 = new() { Name = "Rifleman" };
            Role role2 = new() { Name = "Trainee" };
            Role role3 = new() { Name = "Marksman" };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> { role1, role2, role3 });

            IEnumerable<Role> subject = _rolesContext.Get();

            subject.Should().ContainInOrder(role3, role1, role2);
        }

        [Fact]
        public void ShouldGetSingleByName()
        {
            Role role1 = new() { Name = "Rifleman" };
            Role role2 = new() { Name = "Trainee" };
            Role role3 = new() { Name = "Marksman" };

            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Role> { role1, role2, role3 });

            Role subject = _rolesContext.GetSingle("Trainee");

            subject.Should().Be(role2);
        }

        [Theory, InlineData(""), InlineData(null)]
        public void ShouldGetNothingWhenNoName(string name)
        {
            _mockDataCollection.Setup(x => x.Get()).Returns(new List<Role>());

            Role subject = _rolesContext.GetSingle(name);

            subject.Should().Be(null);
        }
    }
}
