using MongoDB.Bson.Serialization;

namespace UKSF.Api.ArmaServer.Models.Persistence;

public static class PersistenceBsonClassMaps
{
    public static void Register()
    {
        BsonClassMap.TryRegisterClassMap<AceMedicalState>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(x => x.AdditionalData).SetSerializer(PlainObjectDictionaryBsonSerializer.Instance);
            }
        );

        BsonClassMap.TryRegisterClassMap<DomainPersistenceSession>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(x => x.Markers).SetSerializer(PlainObjectNestedListBsonSerializer.Instance);
                cm.GetMemberMap(x => x.CustomData).SetSerializer(PlainObjectDictionaryBsonSerializer.Instance);
            }
        );
    }
}
