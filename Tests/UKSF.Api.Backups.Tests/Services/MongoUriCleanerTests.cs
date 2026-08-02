using FluentAssertions;
using UKSF.Api.Backups.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class MongoUriCleanerTests
{
    [Theory]
    [InlineData(
        "mongodb://prod:hunter2@localhost:52005/prod?replicaSet=rs0&authSource=admin",
        "mongodb://prod@localhost:52005/?replicaSet=rs0&authSource=admin"
    )]
    [InlineData("mongodb://prod:hunter2@localhost:52005/?authSource=admin", "mongodb://prod@localhost:52005/?authSource=admin")]
    [InlineData("mongodb://prod:hunter2@localhost:52005/prod", "mongodb://prod@localhost:52005/")]
    [InlineData("mongodb://prod:p%40ss%3Aword@localhost:52005/prod?authSource=admin", "mongodb://prod@localhost:52005/?authSource=admin")]
    [InlineData("mongodb://prod@localhost:52005/?authSource=admin", "mongodb://prod@localhost:52005/?authSource=admin")]
    public void The_password_and_the_database_are_stripped(string uri, string expected)
    {
        MongoUriCleaner.ForDump(uri).Should().Be(expected);
    }

    [Fact]
    public void A_replica_set_of_several_hosts_survives()
    {
        var uri = "mongodb://prod:hunter2@one:52005,two:52005,three:52005/prod?replicaSet=rs0&authSource=admin";

        MongoUriCleaner.ForDump(uri).Should().Be("mongodb://prod@one:52005,two:52005,three:52005/?replicaSet=rs0&authSource=admin");
    }

    [Fact]
    public void An_srv_connection_string_is_handled()
    {
        var uri = "mongodb+srv://prod:hunter2@cluster.example.com/prod?authSource=admin";

        MongoUriCleaner.ForDump(uri).Should().Be("mongodb+srv://prod@cluster.example.com/?authSource=admin");
    }

    [Fact]
    public void The_result_never_carries_the_password()
    {
        MongoUriCleaner.ForDump("mongodb://prod:hunter2@localhost:52005/prod?authSource=admin").Should().NotContain("hunter2");
    }
}
