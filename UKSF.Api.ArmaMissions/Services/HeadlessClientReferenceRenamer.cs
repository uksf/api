using System.Text;
using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public static class HeadlessClientReferenceRenamer
{
    private const int SamplesPerHcCap = 20;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sqf",
        ".sqm",
        ".hpp",
        ".cpp",
        ".h",
        ".inc",
        ".ext",
        ".sqs"
    };

    public static void Rename(MissionPatchContext context, IReadOnlyList<string> keptSlotNames, IReadOnlyList<string> serverNames)
    {
        var renameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < keptSlotNames.Count; i++)
        {
            var oldName = keptSlotNames[i];
            var newName = serverNames[i];
            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                renameMap[oldName] = newName;
            }
        }

        if (renameMap.Count == 0)
        {
            return;
        }

        var bareRegex = new Regex("\\b(" + string.Join("|", renameMap.Keys.Select(Regex.Escape)) + ")\\b", RegexOptions.Compiled);
        var hcNameSet = new HashSet<string>(renameMap.Keys, StringComparer.Ordinal);

        var perHcCounts = renameMap.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
        var perHcSamples = renameMap.Keys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);
        var perHcSampleKeys = renameMap.Keys.ToDictionary(k => k, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var file in EnumerateTextFiles(context.FolderPath))
        {
            var rel = Path.GetRelativePath(context.FolderPath, file).Replace('\\', '/');
            var content = SqmTextEditor.ReadFile(file);
            var fileLines = content.Split('\n');
            var changed = false;
            var inBlockComment = false;

            for (var i = 0; i < fileLines.Length; i++)
            {
                var (newLine, matched) = ProcessLine(fileLines[i], bareRegex, hcNameSet, renameMap, ref inBlockComment);
                if (matched.Count == 0)
                {
                    continue;
                }

                fileLines[i] = newLine;
                changed = true;
                var trimmed = fileLines[i].TrimEnd('\r').Trim();
                var seenInLine = new HashSet<string>(matched, StringComparer.Ordinal);

                foreach (var name in matched)
                {
                    perHcCounts[name]++;
                }

                foreach (var name in seenInLine)
                {
                    var sampleKey = $"{rel}|{trimmed}";
                    if (perHcSampleKeys[name].Add(sampleKey))
                    {
                        perHcSamples[name].Add($"{rel}:{i + 1}: {trimmed}");
                    }
                }
            }

            if (changed)
            {
                SqmTextEditor.WriteFile(file, string.Join('\n', fileLines));
            }
        }

        if (perHcCounts.Values.All(c => c == 0))
        {
            return;
        }

        var detail = new StringBuilder();
        detail.AppendLine("Mission references to headless client slot names were rewritten to match the server's configured names.");
        detail.AppendLine("Verify the listed locations if you suspect false positives:");
        detail.AppendLine();

        foreach (var oldName in renameMap.Keys)
        {
            var count = perHcCounts[oldName];
            if (count == 0)
            {
                continue;
            }

            detail.AppendLine($"{oldName} -> {renameMap[oldName]} ({count} occurrence{(count == 1 ? string.Empty : "s")}):");

            var samples = perHcSamples[oldName];
            foreach (var sample in samples.Take(SamplesPerHcCap))
            {
                detail.AppendLine($"  {sample}");
            }

            if (samples.Count > SamplesPerHcCap)
            {
                detail.AppendLine($"  ...and {samples.Count - SamplesPerHcCap} more");
            }

            detail.AppendLine();
        }

        context.Reports.Add(new ValidationReport("Headless client references rewritten", detail.ToString().TrimEnd()));
    }

    private static (string NewLine, List<string> Matched) ProcessLine(
        string line,
        Regex bareRegex,
        HashSet<string> hcNameSet,
        Dictionary<string, string> renameMap,
        ref bool inBlockComment
    )
    {
        var sb = new StringBuilder(line.Length);
        var matched = new List<string>();
        var i = 0;

        while (i < line.Length)
        {
            if (inBlockComment)
            {
                var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    sb.Append(line, i, line.Length - i);
                    return (sb.ToString(), matched);
                }

                sb.Append(line, i, end - i + 2);
                i = end + 2;
                inBlockComment = false;
                continue;
            }

            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
            {
                sb.Append(line, i, line.Length - i);
                return (sb.ToString(), matched);
            }

            if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
            {
                sb.Append("/*");
                i += 2;
                inBlockComment = true;
                continue;
            }

            var c = line[i];
            if (c == '"' || c == '\'')
            {
                var j = i + 1;
                while (j < line.Length)
                {
                    if (line[j] == c)
                    {
                        if (j + 1 < line.Length && line[j + 1] == c)
                        {
                            j += 2;
                            continue;
                        }

                        j++;
                        break;
                    }

                    j++;
                }

                var contentStart = i + 1;
                var contentEnd = j > 0 && j <= line.Length && line[j - 1] == c ? j - 1 : j;
                if (contentEnd > contentStart)
                {
                    var content = line.Substring(contentStart, contentEnd - contentStart);
                    if (hcNameSet.Contains(content))
                    {
                        sb.Append(c);
                        sb.Append(renameMap[content]);
                        sb.Append(c);
                        matched.Add(content);
                        i = j;
                        continue;
                    }
                }

                sb.Append(line, i, j - i);
                i = j;
                continue;
            }

            var next = line.Length;
            for (var k = i; k < line.Length; k++)
            {
                var ck = line[k];
                if (ck == '"' || ck == '\'')
                {
                    next = k;
                    break;
                }

                if (ck == '/' && k + 1 < line.Length && (line[k + 1] == '/' || line[k + 1] == '*'))
                {
                    next = k;
                    break;
                }
            }

            var span = line.Substring(i, next - i);
            sb.Append(
                bareRegex.Replace(
                    span,
                    m =>
                    {
                        matched.Add(m.Value);
                        return renameMap[m.Value];
                    }
                )
            );
            i = next;
        }

        return (sb.ToString(), matched);
    }

    private static IEnumerable<string> EnumerateTextFiles(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (TextExtensions.Contains(Path.GetExtension(file)))
            {
                yield return file;
            }
        }
    }
}
