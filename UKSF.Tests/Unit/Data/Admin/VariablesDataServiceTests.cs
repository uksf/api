using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Admin.Models;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using Xunit;

namespace UKSF.Tests.Unit.Data.Admin {
    public class VariablesDataServiceTests {
        private readonly Mock<IDataCollection<VariableItem>> mockDataCollection;
        private readonly VariablesDataService variablesDataService;
        private List<VariableItem> mockCollection;

        public VariablesDataServiceTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<IDataEventBus<VariableItem>> mockDataEventBus = new Mock<IDataEventBus<VariableItem>>();
            mockDataCollection = new Mock<IDataCollection<VariableItem>>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<VariableItem>(It.IsAny<string>())).Returns(mockDataCollection.Object);
            mockDataCollection.Setup(x => x.Get()).Returns(() => mockCollection);

            variablesDataService = new VariablesDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Theory, InlineData(""), InlineData("game path")]
        public void ShouldGetNothingWhenNoKeyOrNotFound(string key) {
            VariableItem item1 = new VariableItem { key = "MISSIONS_PATH" };
            VariableItem item2 = new VariableItem { key = "SERVER_PATH" };
            VariableItem item3 = new VariableItem { key = "DISCORD_IDS" };
            mockCollection = new List<VariableItem> { item1, item2, item3 };

            VariableItem subject = variablesDataService.GetSingle(key);

            subject.Should().Be(null);
        }

        [Theory, InlineData(""), InlineData(null)]
        public async Task ShouldThrowForUpdateWhenNoKeyOrNull(string key) {
            mockCollection = new List<VariableItem>();

            Func<Task> act = async () => await variablesDataService.Update(key, "75");

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Theory, InlineData(""), InlineData(null)]
        public async Task ShouldThrowForDeleteWhenNoKeyOrNull(string key) {
            mockCollection = new List<VariableItem>();

            Func<Task> act = async () => await variablesDataService.Delete(key);

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        [Fact]
        public async Task ShouldDeleteItem() {
            VariableItem item1 = new VariableItem { key = "DISCORD_ID", item = "50" };
            mockCollection = new List<VariableItem> { item1 };

            mockDataCollection.Setup(x => x.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask).Callback((string id) => mockCollection.RemoveAll(x => x.id == id));

            await variablesDataService.Delete("discord id");

            mockCollection.Should().HaveCount(0);
        }

        [Fact]
        public void ShouldGetItemByKey() {
            VariableItem item1 = new VariableItem { key = "MISSIONS_PATH" };
            VariableItem item2 = new VariableItem { key = "SERVER_PATH" };
            VariableItem item3 = new VariableItem { key = "DISCORD_IDS" };
            mockCollection = new List<VariableItem> { item1, item2, item3 };

            VariableItem subject = variablesDataService.GetSingle("server path");

            subject.Should().Be(item2);
        }

        [Fact]
        public void Should_get_collection_in_order() {
            VariableItem item1 = new VariableItem { key = "MISSIONS_PATH" };
            VariableItem item2 = new VariableItem { key = "SERVER_PATH" };
            VariableItem item3 = new VariableItem { key = "DISCORD_IDS" };
            mockCollection = new List<VariableItem> { item1, item2, item3 };

            IEnumerable<VariableItem> subject = variablesDataService.Get();

            subject.Should().ContainInOrder(item3, item1, item2);
        }

        [Fact]
        public async Task ShouldUpdateItemValue() {
            VariableItem subject = new VariableItem { key = "DISCORD_ID", item = "50" };
            mockCollection = new List<VariableItem> { subject };

            mockDataCollection.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateDefinition<VariableItem>>()))
                              .Returns(Task.CompletedTask)
                              .Callback((string id, UpdateDefinition<VariableItem> _) => mockCollection.First(x => x.id == id).item = "75");

            await variablesDataService.Update("discord id", "75");

            subject.item.Should().Be("75");
        }
    }
}
