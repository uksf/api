using EphemeralMongo;

namespace UKSF.Api.Tests.Common;

public static class TestMongo
{
    public static IMongoRunner Start()
    {
        return MongoRunner.Run(
            new MongoRunnerOptions
            {
                Version = MongoVersion.V7,
                AdditionalArguments = ["--quiet", "--wiredTigerCacheSizeGB", "0.25", "--setParameter", "diagnosticDataCollectionEnabled=false"]
            }
        );
    }
}
