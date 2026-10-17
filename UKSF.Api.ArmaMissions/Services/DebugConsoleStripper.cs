using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IDebugConsoleStripper
{
    void Patch(MissionPatchContext context);
}

public class DebugConsoleStripper : IDebugConsoleStripper
{
    private const string DebugConsole = "EnableDebugConsole";

    public void Patch(MissionPatchContext context)
    {
        if (context.SqmPath is null || !File.Exists(context.SqmPath))
        {
            return;
        }

        var text = SqmTextEditor.ReadFile(context.SqmPath);
        var root = SqmParser.Parse(text);
        var editor = new SqmTextEditor(text);

        foreach (var node in root.Descendants.Prepend(root))
        {
            foreach (var property in node.Properties.Where(p => p.Name.Equals(DebugConsole, StringComparison.OrdinalIgnoreCase)))
            {
                editor.DeleteLines(property.Start, property.End);
            }

            RemoveDebugConsoleAttributes(node, editor);
        }

        if (editor.HasEdits)
        {
            SqmTextEditor.WriteFile(context.SqmPath, editor.Apply());
        }
    }

    private static void RemoveDebugConsoleAttributes(SqmClassNode node, SqmTextEditor editor)
    {
        var attributes = node.Classes.Where(IsNumberedAttribute).ToList();
        var debugConsole = attributes.Where(a => a.Value("property").Equals(DebugConsole, StringComparison.OrdinalIgnoreCase)).ToHashSet();
        if (debugConsole.Count == 0)
        {
            return;
        }

        foreach (var attribute in debugConsole)
        {
            editor.DeleteLines(attribute.Start, attribute.End);
        }

        var remaining = attributes.Where(a => !debugConsole.Contains(a)).ToList();
        for (var i = 0; i < remaining.Count; i++)
        {
            editor.Rename(remaining[i], $"Attribute{i}");
        }

        if (node.Property("nAttributes") is { } count)
        {
            editor.SetValue(count, remaining.Count.ToString());
        }
    }

    private static bool IsNumberedAttribute(SqmClassNode node) =>
        node.Name.StartsWith("Attribute", StringComparison.OrdinalIgnoreCase) && int.TryParse(node.Name["Attribute".Length..], out _);
}
