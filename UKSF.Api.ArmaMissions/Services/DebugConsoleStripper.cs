using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IDebugConsoleStripper
{
    void Patch(MissionPatchContext context);
}

public class DebugConsoleStripper : IDebugConsoleStripper
{
    private static readonly Regex ScalarLine = new(@"^\s*enableDebugConsole\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AttributeProperty = new(@"property\s*=\s*""EnableDebugConsole""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Patch(MissionPatchContext context)
    {
        if (context.SqmPath is null || !File.Exists(context.SqmPath))
        {
            return;
        }

        var lines = File.ReadAllLines(context.SqmPath).ToList();
        var changed = false;

        if (RemoveScalarLines(lines))
        {
            changed = true;
        }

        if (RemoveEnableDebugConsoleAttributeBlocks(lines))
        {
            changed = true;
        }

        if (changed)
        {
            File.WriteAllLines(context.SqmPath, lines);
        }
    }

    private static bool RemoveScalarLines(List<string> lines)
    {
        var changed = false;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (ScalarLine.IsMatch(lines[i]))
            {
                lines.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private static bool RemoveEnableDebugConsoleAttributeBlocks(List<string> lines)
    {
        var changed = false;
        var i = 0;
        while (i < lines.Count)
        {
            if (!lines[i].TrimStart().StartsWith("class Attribute", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            var blockEnd = FindBlockEnd(lines, i);
            if (blockEnd == -1)
            {
                i++;
                continue;
            }

            var blockMatchesEnableDebugConsole = false;
            for (var j = i + 1; j < blockEnd; j++)
            {
                if (AttributeProperty.IsMatch(lines[j]))
                {
                    blockMatchesEnableDebugConsole = true;
                    break;
                }
            }

            if (blockMatchesEnableDebugConsole)
            {
                lines.RemoveRange(i, blockEnd - i + 1);
                changed = true;
                continue;
            }

            i = blockEnd + 1;
        }

        return changed;
    }

    private static int FindBlockEnd(List<string> lines, int classLineIndex)
    {
        var index = classLineIndex + 1;
        if (index >= lines.Count || lines[index].Trim() != "{")
        {
            return -1;
        }

        var depth = 1;
        index++;
        while (index < lines.Count && depth > 0)
        {
            var trimmed = lines[index].Trim();
            if (trimmed == "{")
            {
                depth++;
            }
            else if (trimmed == "};")
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }

            index++;
        }

        return -1;
    }
}
