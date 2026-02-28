using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmWriter
{
    void Write(MissionPatchContext context);
}

public class SqmWriter : ISqmWriter
{
    public void Write(MissionPatchContext context)
    {
        var lines = new List<string>();
        lines.AddRange(context.Sqm.HeaderLines);
        lines.AddRange(SerializeEntities(context.Sqm.Entities));
        lines.AddRange(context.Sqm.FooterLines);

        lines = lines.Select(x => x.RemoveTrailingNewLineGroup().RemoveNewLines().RemoveEmbeddedQuotes()).ToList();
        File.WriteAllLines(context.SqmPath, lines);
    }

    private static List<string> SerializeEntities(List<SqmEntity> entities)
    {
        List<string> serialized = ["class Entities", "{", $"items = {entities.Count};"];
        foreach (var entity in entities)
        {
            serialized.AddRange(SerializeEntity(entity));
        }

        serialized.Add("};");
        return serialized;
    }

    private static List<string> SerializeEntity(SqmEntity entity)
    {
        return entity switch
        {
            SqmGroup group => SerializeGroup(group),
            _              => entity.RawLines
        };
    }

    private static List<string> SerializeGroup(SqmGroup group)
    {
        if (group.IsIgnored || group.Children.Count == 0)
        {
            return group.RawLines;
        }

        var lines = new List<string>(group.RawLines);
        var entitiesIndex = SqmParsingUtilities.GetIndexByKey(lines, "Entities");
        if (entitiesIndex == -1)
        {
            return lines;
        }

        var entitiesBlock = SqmParsingUtilities.ReadBlock(lines, entitiesIndex);
        lines.RemoveRange(entitiesIndex, entitiesBlock.Count);
        lines.InsertRange(entitiesIndex, SerializeEntities(group.Children));

        return lines;
    }
}
