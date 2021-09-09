// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using FluentAssertions;
// using Mongo2Go;
// using MongoDB.Bson;
// using MongoDB.Driver;
// using UKSF.Api.Base.Context;
// using UKSF.Api.Personnel.Models;
// using UKSF.Tests.Common;
// using Xunit;

// // Available test collections as json:
// // accounts
// // commentThreads
// // discharges
// // gameServers
// // ranks
// // roles
// // scheduledJobs
// // teamspeakSnapshots
// // units
// // variables

// namespace UKSF.Tests.Integration.Data {
// public class DataCollectionTests : IDisposable {
// private const string TEST_COLLECTION_NAME = "roles";
// private MongoDbRunner _mongoDbRunner;

// public void Dispose() {
// _mongoDbRunner?.Dispose();
// }

// private async Task MongoTest(Func<IMongoDatabase, Task> testFunction) {
// _mongoDbRunner = MongoDbRunner.Start(additionalMongodArguments: "--quiet");

// IMongoDatabase database = MongoClientFactory.GetDatabase($"{_mongoDbRunner.ConnectionString}{Guid.NewGuid()}");

// await testFunction(database);

// _mongoDbRunner.Dispose();
// }

// private static async Task<(MongoCollection<Role> dataCollection, string testId)> SetupTestCollection(IMongoDatabase database) {
// MongoCollection<Role> mongoCollection = new(database, TEST_COLLECTION_NAME);
// await mongoCollection.AssertCollectionExistsAsync();

// string testId = ObjectId.GenerateNewId().ToString();
// List<Role> roles = new() {
// new Role { Name = "Rifleman" },
// new Role { Name = "Trainee" },
// new Role { Name = "Marksman", Id = testId },
// new Role { Name = "1iC", RoleType = RoleType.UNIT, Order = 0 },
// new Role { Name = "2iC", RoleType = RoleType.UNIT, Order = 1 },
// new Role { Name = "NCOiC", RoleType = RoleType.UNIT, Order = 3 },
// new Role { Name = "NCOiC Air Troop", RoleType = RoleType.INDIVIDUAL, Order = 0 }
// };
// roles.ForEach(x => mongoCollection.AddAsync(x).GetAwaiter().GetResult());

// return (mongoCollection, testId);
// }

// [Fact]
// public async Task Should_add_item() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// Role role = new() { Name = "Section Leader" };
// await dataCollection.AddAsync(role);

// List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

// subject.Should().Contain(x => x.Name == role.Name);
// }
// );
// }

// [Fact]
// public async Task Should_create_collection() {
// await MongoTest(
// async database => {
// MongoCollection<TestDataModel> mongoCollection = new(database, "test");

// await mongoCollection.AssertCollectionExistsAsync();

// MongoDB.Driver.IMongoCollection<TestDataModel> subject = database.GetCollection<TestDataModel>("test");

// subject.Should().NotBeNull();
// }
// );
// }

// [Fact]
// public async Task Should_delete_item_by_id() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

// await dataCollection.DeleteAsync(testId);

// List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

// subject.Should().NotContain(x => x.Id == testId);
// }
// );
// }

// [Fact]
// public async Task Should_delete_many_by_predicate() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// await dataCollection.DeleteManyAsync(x => x.Order == 0);

// List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().ToList();

// subject.Should().NotContain(x => x.Order == 0);
// }
// );
// }

// [Fact]
// public async Task Should_get_many_by_predicate() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// List<Role> subject = dataCollection.Get(x => x.Order == 0).ToList();

// subject.Should().NotBeNull();
// subject.Count.Should().Be(5);
// subject.Should().Contain(x => x.Name == "Trainee");
// }
// );
// }

// [Fact]
// public async Task Should_get_collection() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// List<Role> subject = dataCollection.Get().ToList();

// subject.Should().NotBeNull();
// subject.Count.Should().Be(7);
// subject.Should().Contain(x => x.Name == "NCOiC");
// }
// );
// }

// [Fact]
// public async Task Should_get_item_by_id() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

// Role subject = dataCollection.GetSingle(testId);

// subject.Should().NotBeNull();
// subject.Name.Should().Be("Marksman");
// }
// );
// }

// [Fact]
// public async Task Should_get_item_by_predicate() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// Role subject = dataCollection.GetSingle(x => x.RoleType == RoleType.UNIT && x.Order == 1);

// subject.Should().NotBeNull();
// subject.Name.Should().Be("2iC");
// }
// );
// }

// [Fact]
// public async Task Should_not_throw_when_collection_exists() {
// await MongoTest(
// async database => {
// await database.CreateCollectionAsync("test");
// MongoCollection<TestDataModel> mongoCollection = new MongoCollection<TestDataModel>(database, "test");

// Func<Task> act = async () => await mongoCollection.AssertCollectionExistsAsync();

// await act.Should().NotThrowAsync<MongoCommandException>();
// }
// );
// }

// [Fact]
// public async Task Should_replace_item() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

// Role role = new Role { Id = testId, Name = "Sharpshooter" };
// await dataCollection.ReplaceAsync(role.Id, role);

// Role subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.Id == testId);

// subject.Name.Should().Be(role.Name);
// subject.Order.Should().Be(0);
// subject.RoleType.Should().Be(RoleType.INDIVIDUAL);
// }
// );
// }

// [Fact]
// public async Task Should_update_item_by_id() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

// await dataCollection.UpdateAsync(testId, Builders<Role>.Update.Set(x => x.Order, 10));

// Rank subject = database.GetCollection<Rank>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.Id == testId);

// subject.Order.Should().Be(10);
// }
// );
// }

// [Fact]
// public async Task Should_update_item_by_filter() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, string testId) = await SetupTestCollection(database);

// await dataCollection.UpdateAsync(Builders<Role>.Filter.Where(x => x.Id == testId), Builders<Role>.Update.Set(x => x.Order, 10));

// Rank subject = database.GetCollection<Rank>(TEST_COLLECTION_NAME).AsQueryable().First(x => x.Id == testId);

// subject.Order.Should().Be(10);
// }
// );
// }

// [Fact]
// public async Task Should_update_many_by_predicate() {
// await MongoTest(
// async database => {
// (MongoCollection<Role> dataCollection, _) = await SetupTestCollection(database);

// await dataCollection.UpdateManyAsync(x => x.Order == 0, Builders<Role>.Update.Set(x => x.Order, 10));

// List<Role> subject = database.GetCollection<Role>(TEST_COLLECTION_NAME).AsQueryable().Where(x => x.Order == 10).ToList();

// subject.Count.Should().Be(5);
// }
// );
// }
// }
// }


