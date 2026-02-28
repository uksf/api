using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmReader
{
    void Read(MissionPatchContext context);
}

public class SqmReader : ISqmReader
{
    public void Read(MissionPatchContext context)
    {
        var allLines = File.ReadAllLines(context.SqmPath).Select(x => x.Trim()).ToList();
        allLines.RemoveAll(string.IsNullOrEmpty);
        StripUnbinHeader(allLines);

        var nextId = ReadNextId(allLines);
        context.NextEntityId = nextId;

        var entitiesIndex = GetIndexByKey(allLines, "class Entities");
        if (entitiesIndex == -1)
        {
            context.Sqm = new SqmDocument { HeaderLines = allLines };
            return;
        }

        var headerLines = allLines.Take(entitiesIndex).ToList();
        var entitiesBlock = ReadBlock(allLines, ref entitiesIndex);
        var footerLines = allLines.Skip(entitiesIndex).ToList();

        var entities = ParseEntities(entitiesBlock);

        context.Sqm = new SqmDocument
        {
            HeaderLines = headerLines,
            Entities = entities,
            FooterLines = footerLines
        };
    }

    private static void StripUnbinHeader(List<string> lines)
    {
        if (lines.Count > 0 && lines[0] == "////////////////////////////////////////////////////////////////////")
        {
            lines.RemoveRange(0, 7);
        }
    }

    private static int ReadNextId(List<string> lines)
    {
        var providerBlock = ReadBlockByKey(lines, "ItemIDProvider");
        if (providerBlock.Count == 0)
        {
            return 0;
        }

        var nextIdValue = ReadSingleValue(providerBlock, "nextID");
        return int.TryParse(nextIdValue, out var id) ? id : 0;
    }

    private static List<SqmEntity> ParseEntities(List<string> entitiesBlock)
    {
        var itemCount = ReadItemCount(entitiesBlock);
        List<SqmEntity> entities = [];
        var index = entitiesBlock.FindIndex(x => x.StartsWith("class Item"));
        while (entities.Count < itemCount && index < entitiesBlock.Count)
        {
            var itemBlock = ReadBlock(entitiesBlock, ref index);
            entities.Add(ParseEntity(itemBlock));
        }

        return entities;
    }

    private static SqmEntity ParseEntity(List<string> rawLines)
    {
        var dataType = ReadSingleValue(rawLines, "dataType");

        switch (dataType)
        {
            case "Group":
            {
                var childEntitiesBlock = ReadBlockByKey(rawLines, "Entities");
                List<SqmEntity> children = [];
                if (childEntitiesBlock.Count > 0)
                {
                    children = ParseEntities(childEntitiesBlock);
                }

                var allChildrenPlayable = children.Count > 0 && children.All(c => c is SqmObject { IsPlayable: true });
                var isIgnored = children.Any(c => c is SqmObject obj && obj.RawLines.Any(l => l.ToLower().Contains("@ignore")));

                return new SqmGroup
                {
                    Children = children,
                    AllChildrenPlayable = allChildrenPlayable,
                    IsIgnored = isIgnored,
                    RawLines = rawLines
                };
            }
            case "Object":
            {
                var isPlayable = ReadSingleValue(rawLines, "isPlayable");
                var isPlayer = ReadSingleValue(rawLines, "isPlayer");
                var playable = isPlayable == "1" || isPlayer == "1";
                var type = ReadSingleValue(rawLines, "type");

                return new SqmObject
                {
                    IsPlayable = playable,
                    Type = type,
                    RawLines = rawLines
                };
            }
            case "Logic":
            {
                var type = ReadSingleValue(rawLines, "type");
                return new SqmLogic { Type = type, RawLines = rawLines };
            }
            default: return new SqmPassthrough { RawLines = rawLines };
        }
    }

    private static int ReadItemCount(List<string> block)
    {
        var value = ReadSingleValue(block, "items");
        return int.TryParse(value, out var count) ? count : 0;
    }

    private static string ReadSingleValue(List<string> lines, string key)
    {
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Replace(";", "").Replace("\"", "").Trim();
            }
        }

        return "";
    }

    private static int GetIndexByKey(List<string> source, string key)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i].ToLower().Contains(key.ToLower()))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> ReadBlock(List<string> source, ref int index)
    {
        List<string> data = [source[index]];
        index++;
        if (index >= source.Count)
        {
            return data;
        }

        var opening = source[index];
        Stack<string> stack = new();
        stack.Push(opening);
        data.Add(opening);
        index++;
        while (stack.Count != 0)
        {
            if (index >= source.Count)
            {
                return [];
            }

            var line = source[index];
            if (line.Equals("{"))
            {
                stack.Push(line);
            }

            if (line.Equals("};"))
            {
                stack.Pop();
            }

            data.Add(line);
            index++;
        }

        return data;
    }

    private static List<string> ReadBlockByKey(List<string> source, string key)
    {
        var index = GetIndexByKey(source, key);
        return index == -1 ? [] : ReadBlock(source, ref index);
    }
}
