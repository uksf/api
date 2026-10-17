using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmWriter
{
    void Write(MissionPatchContext context);
}

public class SqmWriter : ISqmWriter
{
    private const string UnbinHeaderFirstLine = "////////////////////////////////////////////////////////////////////";
    private const int UnbinHeaderLineCount = 7;

    public void Write(MissionPatchContext context)
    {
        var sqm = context.Sqm;
        var entitiesNode = sqm.EntitiesNode ?? throw new SqmSyntaxException("mission.sqm has no class Mission with class Entities");
        var editor = new SqmTextEditor(sqm.Text);

        var kept = sqm.Entities.Where(e => e.Node is not null).Select(e => e.Node).ToHashSet();
        foreach (var node in entitiesNode.Items.Where(node => !kept.Contains(node)))
        {
            editor.DeleteLines(node.Start, node.End);
        }

        List<string> added = [];
        var itemsProperty = entitiesNode.Property("items");
        if (itemsProperty is null)
        {
            added.Add($"items={sqm.Entities.Count};");
        }
        else
        {
            editor.SetValue(itemsProperty, sqm.Entities.Count.ToString());
        }

        for (var i = 0; i < sqm.Entities.Count; i++)
        {
            var entity = sqm.Entities[i];
            if (entity.Node is not null)
            {
                editor.Rename(entity.Node, $"Item{i}");
            }
            else
            {
                added.Add($"class Item{i}");
                added.AddRange(entity.Lines.Skip(1));
            }
        }

        if (added.Count > 0)
        {
            var firstItem = entitiesNode.Items.FirstOrDefault();
            var indent = firstItem is null ? editor.IndentAt(entitiesNode.BodyClose) + "\t" : editor.IndentAt(firstItem.Start);
            editor.InsertLinesBefore(entitiesNode.BodyClose, added, indent);
        }

        var nextId = SqmReader.NextIdProperty(sqm.Root);
        if (nextId is not null && (!int.TryParse(nextId.Text, out var current) || current < context.NextEntityId))
        {
            editor.SetValue(nextId, context.NextEntityId.ToString());
        }

        var unbinHeaderEnd = UnbinHeaderEnd(sqm.Text);
        if (unbinHeaderEnd > 0)
        {
            editor.Replace(0, unbinHeaderEnd, "");
        }

        SqmTextEditor.WriteFile(context.SqmPath, editor.Apply());
    }

    private static int UnbinHeaderEnd(string text)
    {
        if (!text.StartsWith(UnbinHeaderFirstLine, StringComparison.Ordinal))
        {
            return 0;
        }

        var end = 0;
        for (var line = 0; line < UnbinHeaderLineCount; line++)
        {
            var newLine = text.IndexOf('\n', end);
            if (newLine < 0)
            {
                return text.Length;
            }

            end = newLine + 1;
        }

        return end;
    }
}
