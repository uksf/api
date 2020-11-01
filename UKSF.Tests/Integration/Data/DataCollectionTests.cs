using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Data;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Services.Utility;
using UKSF.Tests.Common;
using Xunit;

// Available test collections as json:
// accounts
// commentThreads
// discharges
// gameServers
// ranks
// roles
// scheduledJobs
// teamspeakSnapshots
// units
// variables

namespace UKSF.Tests.Integration.Data {
    public class DataCollectionTests : IDisposable {
        private const string TEST_COLLECTION_NAME = "roles";
        private MongoDbRunner mongoDbRunner;

        public void Dispose() {
            mongoDbRunner?.Dispose();
        }

        private async Task MongoTest(Func<IMongoDatabase, Task> testFunction) {
            mongoDbRunner = MongoDbRunner.Start(additionalMongodArguments: "--quiet");

            IMongoDatabase database = MongoClientFactory.GetDatabase($"{mongoDbRunner.ConnectionString}{Guid.NewGuid()}");

            await testFunction(database);

            mongoDbRunner.Dispose();
        }

        private static async Task<(DataCollection<Role> dataCollection, string testId)> SetupTestCollection(IMongoDatabase database) {
            DataCollection<Role> dataCollection = new DataCollection<Role>(database, TEST_COLLECTION_NAME);
            await dataCollection.AssertCollectionExistsAsync();

            string testId = ObjectId.GenerateNewId().ToString();
            List<Role> roles = new List<Role> {
                new Role { name = "Rifleman" },
                new Role { name = "Trainee" },
                new Role { name = "Marksman", id = testId },
                new Role { name = "1iC", roleType = RoleType.UNIT, order = 0 },
                new Role { name = "2iC", roleType = RoleType.UNIT, order = 1 },
                new Role { name = "NCOiC", roleType = RoleType.UNIT, order = 3 },
                new Role { name = "NCOiC Air Troop", roleType = RoleType.INDIVIDUAL, order = 0 }
            };
            roles.ForEach(x => dataCollection.AddAsync(x).Wait());

            return (dataCollection, testId);
        }

        [Fact]
        public async Task Should_add_item() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    Role role = new Role { name = "Section Leader" };
                    await dataCollection.AddAsync(role);

                    List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().Contain(x => x.name == role.name);
                }
            );
        }

        [Fact]
        public async Task Should_create_collection() {
            await MongoTest(
                async database => {
                    DataCollection<TestDataModel> dataCollection = new DataCollection<TestDataModel>(database, "test");

                    await dataCollection.AssertCollectionExistsAsync();

                    IMongoCollection<TestDataModel> subject = database.GetCollection<TestDataModel>("test");

                    subject.Should().NotBeNull();
                }
            );
        }

        [Fact]
        public async Task Should_delete_item_by_id() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

                    await dataCollection.DeleteAsync(testId);

                    List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().NotContain(x => x.id == testId);
                }
            );
        }

        [Fact]
        public async Task Should_delete_many_by_predicate() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    await dataCollection.DeleteManyAsync(x => x.order == 0);

                    List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().NotContain(x => x.order == 0);
                }
            );
        }

        [Fact]
        public async Task Should_get_many_by_predicate() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    List<Role> subject = dataCollection.Get(x => x.order == 0).ToList();

                    subject.Should().NotBeNull();
                    subject.Count.Should().Be(5);
                    subject.Should().Contain(x => x.name == "Trainee");
                }
            );
        }

        [Fact]
        public async Task Should_get_collection() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    List<Role> subject = dataCollection.Get().ToList();

                    subject.Should().NotBeNull();
                    subject.Count.Should().Be(7);
                    subject.Should().Contain(x => x.name == "NCOiC");
                }
            );
        }

        [Fact]
        public async Task Should_get_item_by_id() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

                    Role subject = dataCollection.GetSingle(testId);

                    subject.Should().NotBeNull();
                    subject.name.Should().Be("Marksman");
                }
            );
        }

        [Fact]
        public async Task Should_get_item_by_predicate() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    Role subject = dataCollection.GetSingle(x => x.roleType == RoleType.UNIT && x.order == 1);

                    subject.Should().NotBeNull();
                    subject.name.Should().Be("2iC");
                }
            );
        }

        [Fact]
        public async Task Should_not_throw_when_collection_exists() {
            await MongoTest(
                async database => {
                    await database.CreateCollectionAsync("test");
                    DataCollection<TestDataModel> dataCollection = new DataCollection<TestDataModel>(database, "test");

                    Func<Task> act = async () => await dataCollection.AssertCollectionExistsAsync();

                    await act.Should().NotThrowAsync<MongoCommandException>();
                }
            );
        }

        [Fact]
        public async Task Should_replace_item() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

                    Role role = new Role { id = testId, name = "Sharpshooter" };
                    await dataCollection.ReplaceAsync(role.id, role);

                    Role subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.id == testId);

                    subject.name.Should().Be(role.name);
                    subject.order.Should().Be(0);
                    subject.roleType.Should().Be(RoleType.INDIVIDUAL);
                }
            );
        }

        [Fact]
        public async Task Should_update_item_by_id() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

                    await dataCollection.UpdateAsync(testId, Builders<Role>.Update.Set(x => x.order, 10));

                    Rank subject = database.GetCollection<Rank>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.id == testId);

                    subject.order.Should().Be(10);
                }
            );
        }

        [Fact]
        public async Task Should_update_item_by_filter() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

                    await dataCollection.UpdateAsync(Builders<Role>.Filter.Where(x => x.id == testId), Builders<Role>.Update.Set(x => x.order, 10));

                    Rank subject = database.GetCollection<Rank>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.id == testId);

                    subject.order.Should().Be(10);
                }
            );
        }

        [Fact]
        public async Task Should_update_many_by_predicate() {
            await MongoTest(
                async database => {
                    (DataCollection<Role> dataCollection, _) = await SetupTestCollection(database);

                    await dataCollection.UpdateManyAsync(x => x.order == 0, Builders<Role>.Update.Set(x => x.order, 10));

                    List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().Where(x => x.order == 10).ToList();

                    subject.Count.Should().Be(5);
                }
            );
        }
    }
}
