// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using FluentAssertions;
// using Mongo2Go;
// using MongoDB.Bson.Serialization.Conventions;
// using MongoDB.Driver;
// using UKSF.Api.Data;
// using UKSF.Api.Models.Integrations;
// using Xunit;
// // ReSharper disable UnusedMember.Global
//
// namespace UKSF.Tests.Unit.Integration.Data {
//     public class DataPerformanceTests {
//         private static async Task MongoTest(Func<MongoDbRunner, IMongoDatabase, Task> testFunction) {
//             MongoDbRunner mongoDbRunner = MongoDbRunner.Start(additionalMongodArguments: "--quiet");
//             ConventionPack conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true) };
//             ConventionRegistry.Register("DefaultConventions", conventionPack, t => true);
//             MongoClient mongoClient = new MongoClient(mongoDbRunner.ConnectionString);
//             IMongoDatabase database = mongoClient.GetDatabase("tests");
//
//             try {
//                 await testFunction(mongoDbRunner, database);
//             } finally {
//                 mongoDbRunner.Dispose();
//             }
//         }
//
//         private static void ImportTestCollection(MongoDbRunner mongoDbRunner, string collectionName) {
//             mongoDbRunner.Import("tests", collectionName, $"../../../testdata/{collectionName}.json", true);
//         }
//
//         // This test tests nothing, and is only used for profiling various data retrieval methods
//         [Fact]
//         public async Task TestGetPerformance() {
//             await MongoTest(
//                 (mongoDbRunner, database) => {
//                     const string COLLECTION_NAME = "teamspeakSnapshots";
//                     ImportTestCollection(mongoDbRunner, COLLECTION_NAME);
//
//                     DataCollection<TeamspeakServerSnapshot> dataCollection = new DataCollection<TeamspeakServerSnapshot>(database, COLLECTION_NAME);
//                     List<TeamspeakServerSnapshot> subject = dataCollection.Get(x => x.timestamp > DateTime.Parse("2018-08-09T05:00:00.307Z"));
//
//                     subject.Should().NotBeNull();
//
//                     return Task.CompletedTask;
//                 }
//             );
//         }
//     }
// }
