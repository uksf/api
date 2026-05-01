using MongoDB.Bson;

namespace UKSF.Api.ArmaServer.Services;

public static class RawEventSplitter
{
    private static readonly HashSet<string> CombatTypes = ["shot", "hit", "kill", "combatDamage", "damageReceived"];

    public record SplitResult(Dictionary<string, List<BsonDocument>> SamplerByUid, List<BsonDocument> Combat, List<BsonDocument> Lifecycle);

    public static SplitResult Split(IEnumerable<BsonDocument> events)
    {
        var samplerByUid = new Dictionary<string, List<BsonDocument>>();
        var combat = new List<BsonDocument>();
        var lifecycle = new List<BsonDocument>();

        foreach (var evt in events)
        {
            var type = evt.GetValue("type", "unknown").AsString;
            if (type == "samplerBatch")
            {
                var uid = evt.GetValue("uid", "").AsString;
                if (string.IsNullOrEmpty(uid)) continue;
                if (!samplerByUid.TryGetValue(uid, out var list))
                {
                    list = [];
                    samplerByUid[uid] = list;
                }

                list.Add(evt);
            }
            else if (CombatTypes.Contains(type))
            {
                combat.Add(evt);
            }
            else
            {
                lifecycle.Add(evt);
            }
        }

        return new SplitResult(samplerByUid, combat, lifecycle);
    }
}
