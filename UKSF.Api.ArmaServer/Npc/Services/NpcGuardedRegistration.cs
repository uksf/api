using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Npc.Models;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Npc.Services;

/// Pure guarded registration parse/validate helpers. No I/O.
public static class NpcGuardedRegistration
{
    public static NpcGuardedState FreshState() =>
        new()
        {
            CooperationBand = NpcCooperationBands.Guarded,
            PendingWarning = false,
            Burned = false,
            DisclosedFactIds = []
        };

    public static (bool Ok, NpcGuardedConfig Config, string Error) ParseAndValidate(
        Dictionary<string, object> data,
        string knowledge,
        NpcPersona persona,
        string mode
    )
    {
        if (!string.Equals(mode, "dynamic", StringComparison.OrdinalIgnoreCase)) return (false, null, "guarded requires mode=dynamic (not scripted)");

        var guardedDict = ToDict(data.GetValueOrDefault("guarded"));
        var concern = ToSafeString(guardedDict.GetValueOrDefault("concern")).Trim();
        if (string.IsNullOrEmpty(concern)) return (false, null, "missing concern");

        var factsRaw = ToList(guardedDict.GetValueOrDefault("facts"));
        if (factsRaw.Count != 3) return (false, null, "exactly three facts required");

        var facts = new List<NpcGuardedFact>(3);
        foreach (var raw in factsRaw)
        {
            var d = ToDict(raw);
            var fact = new NpcGuardedFact
            {
                Id = ToSafeString(d.GetValueOrDefault("id")).Trim(),
                Topic = ToSafeString(d.GetValueOrDefault("topic")).Trim(),
                Text = ToSafeString(d.GetValueOrDefault("text")).Trim()
            };
            if (string.IsNullOrEmpty(fact.Id) || string.IsNullOrEmpty(fact.Topic) || string.IsNullOrEmpty(fact.Text))
                return (false, null, "each fact needs non-empty id, topic, and text");
            facts.Add(fact);
        }

        if (facts.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != 3) return (false, null, "duplicate fact ids");
        if (facts.Select(f => f.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) return (false, null, "duplicate fact text");

        var leakSurfaces =
            new[] { knowledge, concern, persona.Name, persona.Role, persona.Language, persona.Mood, persona.AttitudeToPlayers }.Concat(
                facts.Select(f => f.Topic)
            );

        foreach (var fact in facts)
        {
            foreach (var surface in leakSurfaces)
            {
                if (!string.IsNullOrEmpty(surface) && surface.Contains(fact.Text, StringComparison.OrdinalIgnoreCase))
                    return (false, null, $"fact text leaks into authoring field (id={fact.Id})");
            }
        }

        return (true, new NpcGuardedConfig { Concern = concern, Facts = facts }, null);
    }

    public static bool ContentEquals(NpcGuardedConfig a, NpcGuardedConfig b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (!string.Equals(a.Concern, b.Concern, StringComparison.Ordinal)) return false;
        if (a.Facts.Count != b.Facts.Count) return false;
        for (var i = 0; i < a.Facts.Count; i++)
        {
            var x = a.Facts[i];
            var y = b.Facts[i];
            if (!string.Equals(x.Id, y.Id, StringComparison.Ordinal)) return false;
            if (!string.Equals(x.Topic, y.Topic, StringComparison.Ordinal)) return false;
            if (!string.Equals(x.Text, y.Text, StringComparison.Ordinal)) return false;
        }

        return true;
    }

    public static bool ToBool(object v) =>
        v switch
        {
            bool b                                               => b,
            string s when bool.TryParse(s, out var p)            => p,
            string s when s is "1" or "true" or "TRUE" or "True" => true,
            int i                                                => i != 0,
            long l                                               => l != 0,
            double d                                             => Math.Abs(d) > double.Epsilon,
            _                                                    => false
        };
}
