using System.Runtime.CompilerServices;
using MongoDB.Bson.Serialization.Conventions;

namespace UKSF.Api.ArmaServer.Tests;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Register the camelCase convention before any class map is built.
        // Static-ctor registration on individual test classes is too late if
        // another test serialises a model first, baking PascalCase paths.
        ConventionRegistry.Register("TestCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
    }
}
