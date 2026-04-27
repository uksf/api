using System.Text;
using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public interface IHeadlessClientPatcher
{
    void Patch(MissionPatchContext context);
}

public class HeadlessClientPatcher(IVariablesService variablesService) : IHeadlessClientPatcher
{
    private const string HeadlessClientType = "HeadlessClient_F";
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

    public void Patch(MissionPatchContext context)
    {
        if (context.SqmPath is null || !File.Exists(context.SqmPath))
        {
            return;
        }

        var variable = variablesService.GetVariable("SERVER_HEADLESS_NAMES");
        if (variable is null)
        {
            return;
        }

        var serverNames = variable.AsArray().Where(n => !string.IsNullOrWhiteSpace(n)).ToArray();
        if (serverNames.Length == 0)
        {
            return;
        }

        var lines = File.ReadAllLines(context.SqmPath).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();

        var hcSlots = FindHeadlessClientSlots(lines);
        if (hcSlots.Count == 0)
        {
            return;
        }

        var slotsToKeep = hcSlots.Take(serverNames.Length).ToList();
        var slotsToDrop = hcSlots.Skip(serverNames.Length).ToList();

        if (slotsToDrop.Count > 0)
        {
            foreach (var slot in slotsToDrop.OrderByDescending(s => s.StartLine))
            {
                lines.RemoveRange(slot.StartLine, slot.EndLineExclusive - slot.StartLine);
            }

            RenormalizeAllScopes(lines);

            context.Reports.Add(
                new ValidationReport(
                    "Excess headless client slots dropped",
                    $"Mission has {hcSlots.Count} headless client slots but the server only configures {serverNames.Length}.\n" +
                    $"Dropped slots in mission order: {string.Join(", ", slotsToDrop.Select(s => s.OriginalName))}\n" +
                    "Reduce the number of HeadlessClient_F slots in the mission to remove this warning."
                )
            );
        }

        File.WriteAllLines(context.SqmPath, lines);

        var renameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < slotsToKeep.Count; i++)
        {
            var oldName = slotsToKeep[i].OriginalName;
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
            var content = File.ReadAllText(file);
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
                File.WriteAllText(file, string.Join('\n', fileLines));
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

    private static List<HcSlot> FindHeadlessClientSlots(List<string> lines)
    {
        var output = new List<HcSlot>();
        var rootIndex = lines.FindIndex(l => l.Equals("class Entities", StringComparison.OrdinalIgnoreCase));
        if (rootIndex < 0)
        {
            return output;
        }

        var rootBlock = SqmParsingUtilities.ReadBlock(lines, rootIndex);
        var rootEnd = rootIndex + rootBlock.Count;
        CollectHcSlots(lines, rootIndex, rootEnd, output);
        return output;
    }

    private static void CollectHcSlots(List<string> lines, int scopeStart, int scopeEnd, List<HcSlot> output)
    {
        var index = scopeStart + 1;
        while (index < scopeEnd)
        {
            if (!lines[index].StartsWith("class Item", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            var blockStart = index;
            var cursor = index;
            var block = SqmParsingUtilities.ReadBlock(lines, ref cursor);
            var blockEndExclusive = cursor;

            var dataType = SqmParsingUtilities.ReadSingleValue(block, "dataType");
            var type = SqmParsingUtilities.ReadSingleValue(block, "type");
            var name = SqmParsingUtilities.ReadSingleValue(block, "name");

            if (string.Equals(type, HeadlessClientType, StringComparison.Ordinal) && !string.IsNullOrEmpty(name))
            {
                output.Add(
                    new HcSlot
                    {
                        OriginalName = name,
                        StartLine = blockStart,
                        EndLineExclusive = blockEndExclusive
                    }
                );
            }
            else if (string.Equals(dataType, "Group", StringComparison.Ordinal))
            {
                for (var k = blockStart + 1; k < blockEndExclusive; k++)
                {
                    if (!lines[k].Equals("class Entities", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nestedBlock = SqmParsingUtilities.ReadBlock(lines, k);
                    var nestedEnd = k + nestedBlock.Count;
                    CollectHcSlots(lines, k, nestedEnd, output);
                    break;
                }
            }

            index = blockEndExclusive;
        }
    }

    private static void RenormalizeAllScopes(List<string> lines)
    {
        var i = 0;
        while (i < lines.Count)
        {
            if (!lines[i].Equals("class Entities", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var scopeStart = i;
            var scopeBlock = SqmParsingUtilities.ReadBlock(lines, scopeStart);
            var scopeEnd = scopeStart + scopeBlock.Count;

            var itemsLine = -1;
            var childItemLines = new List<int>();
            var j = scopeStart + 1;
            while (j < scopeEnd)
            {
                var line = lines[j];
                if (line.StartsWith("items=", StringComparison.Ordinal))
                {
                    itemsLine = j;
                    j++;
                    continue;
                }

                if (line.StartsWith("class Item", StringComparison.Ordinal))
                {
                    childItemLines.Add(j);
                    var tmp = j;
                    SqmParsingUtilities.ReadBlock(lines, ref tmp);
                    j = tmp;
                    continue;
                }

                j++;
            }

            for (var k = 0; k < childItemLines.Count; k++)
            {
                lines[childItemLines[k]] = $"class Item{k}";
            }

            if (itemsLine != -1)
            {
                lines[itemsLine] = $"items={childItemLines.Count};";
            }

            i = scopeStart + 1;
        }
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

    private sealed class HcSlot
    {
        public string OriginalName { get; init; }
        public int StartLine { get; init; }
        public int EndLineExclusive { get; init; }
    }
}
