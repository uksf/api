using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using UKSF.Api.Data;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Utility;
using UKSF.Tests.Unit.Common;
using Xunit;

// Available test collections:
// accounts
// commentThreads
// discharges
// gameServers
// ranks
// roles
// scheduledJobs
// scheduledJobsIntegrations
// teamspeakSnapshots
// units
// variables

namespace UKSF.Tests.Unit.Integration.Data {
    public class DataCollectionTests {
        private static async Task MongoTest(Func<MongoDbRunner, IMongoDatabase, Task> testFunction) {
            MongoDbRunner mongoDbRunner = MongoDbRunner.Start();
            ConventionPack conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true) };
            ConventionRegistry.Register("DefaultConventions", conventionPack, t => true);
            MongoClient mongoClient = new MongoClient(mongoDbRunner.ConnectionString);
            IMongoDatabase database = mongoClient.GetDatabase("tests");

            try {
                await testFunction(mongoDbRunner, database);
            } finally {
                mongoDbRunner.Dispose();
            }
        }

        private static void ImportTestCollection(MongoDbRunner mongoDbRunner, string collectionName) {
            mongoDbRunner.Import("tests", collectionName, $"../../../testdata/{collectionName}.json", true);
        }

        [Fact]
        public async Task ShouldAddItem() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "ranks";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Rank> dataCollection = new DataCollection<Rank>(database, COLLECTION_NAME);

                    Rank rank = new Rank { name = "Captain" };
                    await dataCollection.AddAsync(rank);

                    List<Rank> subject = database.GetCollection<Rank>(COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().Contain(x => x.name == rank.name);
                }
            );
        }

        [Fact]
        public async Task ShouldCreateCollection() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    DataCollection<MockDataModel> dataCollection = new DataCollection<MockDataModel>(database, "test");

                    await dataCollection.AssertCollectionExistsAsync();

                    IMongoCollection<MockDataModel> subject = database.GetCollection<MockDataModel>("test");

                    subject.Should().NotBeNull();
                }
            );
        }

        [Fact]
        public async Task ShouldDelete() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "roles";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Role> dataCollection = new DataCollection<Role>(database, COLLECTION_NAME);

                    await dataCollection.DeleteAsync("5b7424eda144bb436484fbc2");

                    List<Role> subject = database.GetCollection<Role>(COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().NotContain(x => x.id == "5b7424eda144bb436484fbc2");
                }
            );
        }

        [Fact]
        public async Task ShouldDeleteMany() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "roles";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Role> dataCollection = new DataCollection<Role>(database, COLLECTION_NAME);

                    await dataCollection.DeleteManyAsync(x => x.order == 0);

                    List<Role> subject = database.GetCollection<Role>(COLLECTION_NAME).AsQueryable().ToList();

                    subject.Should().NotContain(x => x.order == 0);
                }
            );
        }

        [Fact]
        public async Task ShouldGetByPredicate() {
            await MongoTest(
                (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "roles";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);
                    DataCollection<Role> dataCollection = new DataCollection<Role>(database, COLLECTION_NAME);

                    List<Role> subject = dataCollection.Get(x => x.order == 0);

                    subject.Should().NotBeNull();
                    subject.Count.Should().Be(5);
                    subject.Should().Contain(x => x.name == "Trainee");

                    return Task.CompletedTask;
                }
            );
        }

        [Fact]
        public async Task ShouldGetCollection() {
            await MongoTest(
                (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "scheduledJobs";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);
                    DataCollection<ScheduledJob> dataCollection = new DataCollection<ScheduledJob>(database, COLLECTION_NAME);

                    List<ScheduledJob> subject = dataCollection.Get();

                    subject.Should().NotBeNull();
                    subject.Count.Should().Be(2);
                    subject.Should().Contain(x => x.action == "PruneLogs");

                    return Task.CompletedTask;
                }
            );
        }

        [Fact]
        public async Task ShouldGetSingleById() {
            await MongoTest(
                (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "scheduledJobs";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);
                    DataCollection<ScheduledJob> dataCollection = new DataCollection<ScheduledJob>(database, COLLECTION_NAME);

                    ScheduledJob subject = dataCollection.GetSingle("5c006212238c46637025fdad");

                    subject.Should().NotBeNull();
                    subject.action.Should().Be("PruneLogs");

                    return Task.CompletedTask;
                }
            );
        }

        [Fact]
        public async Task ShouldGetSingleByPredicate() {
            await MongoTest(
                (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "scheduledJobs";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);
                    DataCollection<ScheduledJob> dataCollection = new DataCollection<ScheduledJob>(database, COLLECTION_NAME);

                    ScheduledJob subject = dataCollection.GetSingle(x => x.type == ScheduledJobType.LOG_PRUNE);

                    subject.Should().NotBeNull();
                    subject.action.Should().Be("PruneLogs");

                    return Task.CompletedTask;
                }
            );
        }

        [Fact]
        public async Task ShouldNotThrowWhenCollectionExists() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    await database.CreateCollectionAsync("test");
                    DataCollection<MockDataModel> dataCollection = new DataCollection<MockDataModel>(database, "test");

                    Func<Task> act = async () => await dataCollection.AssertCollectionExistsAsync();

                    act.Should().NotThrow<MongoCommandException>();
                }
            );
        }

        [Fact]
        public async Task ShouldReplace() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "roles";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Role> dataCollection = new DataCollection<Role>(database, COLLECTION_NAME);

                    Role role = new Role { id = "5b7424eda144bb436484fbc2", name = "Sharpshooter" };
                    await dataCollection.ReplaceAsync(role.id, role);

                    Role subject = database.GetCollection<Role>(COLLECTION_NAME).AsQueryable().First(x => x.id == "5b7424eda144bb436484fbc2");

                    subject.name.Should().Be(role.name);
                    subject.order.Should().Be(0);
                    subject.roleType.Should().Be(RoleType.INDIVIDUAL);
                }
            );
        }

        [Fact]
        public async Task ShouldUpdate() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "ranks";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Rank> dataCollection = new DataCollection<Rank>(database, COLLECTION_NAME);

                    await dataCollection.UpdateAsync("5b72fbb52d54990cec7c4b24", Builders<Rank>.Update.Set(x => x.order, 10));

                    Rank subject = database.GetCollection<Rank>(COLLECTION_NAME).AsQueryable().First(x => x.id == "5b72fbb52d54990cec7c4b24");

                    subject.order.Should().Be(10);
                }
            );
        }

        [Fact]
        public async Task ShouldUpdateMany() {
            await MongoTest(
                async (mongoDbRunner, database) => {
                    const string COLLECTION_NAME = "roles";
                    ImportTestCollection(mongoDbRunner, COLLECTION_NAME);

                    DataCollection<Role> dataCollection = new DataCollection<Role>(database, COLLECTION_NAME);

                    await dataCollection.UpdateManyAsync(x => x.order == 0, Builders<Role>.Update.Set(x => x.order, 10));

                    List<Role> subject = database.GetCollection<Role>(COLLECTION_NAME).AsQueryable().Where(x => x.order == 10).ToList();

                    subject.Count.Should().Be(5);
                }
            );
        }
    }
}
