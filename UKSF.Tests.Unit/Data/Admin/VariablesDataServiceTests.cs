using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Data.Admin;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Admin;
using Xunit;

namespace UKSF.Tests.Unit.Data.Admin {
    public class VariablesDataServiceTests {
        private readonly VariablesDataService variablesDataService;
        private readonly Mock<IDataCollection> mockDataCollection;
        private List<VariableItem> mockCollection;

        public VariablesDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<IVariablesDataService>> mockdataEventBus = new Mock<IDataEventBus<IVariablesDataService>>();
            variablesDataService = new VariablesDataService(mockDataCollection.Object, mockdataEventBus.Object);

            mockCollection = new List<VariableItem>();

            mockDataCollection.Setup(x => x.Get<VariableItem>()).Returns(() => mockCollection);
        }

        [Fact]
        public void ShouldGetOrderedCollection() {
            VariableItem item1 = new VariableItem {key = "MISSIONS_PATH"};
            VariableItem item2 = new VariableItem {key = "SERVER_PATH"};
            VariableItem item3 = new VariableItem {key = "DISCORD_IDS"};
            mockCollection = new List<VariableItem> {item1, item2, item3};

            List<VariableItem> subject = variablesDataService.Get();

            subject.Should().ContainInOrder(item3, item1, item2);
        }

        [Fact]
        public void ShouldGetItemByKey() {
            VariableItem item1 = new VariableItem {key = "MISSIONS_PATH"};
            VariableItem item2 = new VariableItem {key = "SERVER_PATH"};
            VariableItem item3 = new VariableItem {key = "DISCORD_IDS"};
            mockCollection = new List<VariableItem> {item1, item2, item3};

            VariableItem subject = variablesDataService.GetSingle("server path");

            subject.Should().Be(item2);
        }

        [Fact]
        public async Task ShouldUpdateItemValue() {
            VariableItem subject = new VariableItem {key = "DISCORD_ID", item = "50"};
            mockCollection = new List<VariableItem> {subject};

            mockDataCollection.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<VariableItem>>())).Returns(Task.CompletedTask).Callback((string id, UpdateDefinition<VariableItem> _) => mockCollection.First(x => x.id == id).item = "75");

            await variablesDataService.Update("discord id", "75");

            subject.item.Should().Be("75");
        }

        [Fact]
        public async Task ShouldDeleteItem() {
            VariableItem item1 = new VariableItem {key = "DISCORD_ID", item = "50"};
            mockCollection = new List<VariableItem> {item1};

            mockDataCollection.Setup(x => x.Delete<VariableItem>(It.IsAny<string>())).Returns(Task.CompletedTask).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await variablesDataService.Delete("discord id");

            mockCollection.Should().HaveCount(0);
        }
    }
}
