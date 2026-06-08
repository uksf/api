using System;
using System.Text.RegularExpressions;

namespace UKSF.Api.ArmaServer.Npc.Services;

public static class VoiceSlug
{
    // Lowercase; runs of non-alphanumerics collapse to a single underscore; trim leading/trailing underscores.
    public static string Slugify(string input)
    {
        var lowered = (input ?? string.Empty).ToLowerInvariant();
        return Regex.Replace(lowered, "[^a-z0-9]+", "_").Trim('_');
    }

    public static string Derive(string displayName, string moodOf, string moodLabel)
    {
        if (!string.IsNullOrWhiteSpace(moodOf))
        {
            // moodOf is the parent voiceId — slugify it too so a raw/dirty value can never break the slug invariant.
            var parent = Slugify(moodOf);
            var label = Slugify(moodLabel);
            if (parent.Length == 0)
            {
                throw new ArgumentException("moodOf produced an empty slug");
            }

            if (label.Length == 0)
            {
                throw new ArgumentException("Mood label produced an empty slug");
            }

            return $"{parent}_{label}";
        }

        var slug = Slugify(displayName);
        if (slug.Length == 0)
        {
            throw new ArgumentException("Display name produced an empty slug");
        }

        return slug;
    }
}
