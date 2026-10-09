using System.Runtime.CompilerServices;
using MongoDB.Bson.Serialization.Conventions;
using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        ConventionRegistry.Register("TestCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
        PersistenceBsonClassMaps.Register();
    }
}
