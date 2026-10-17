using System.Text;

namespace UKSF.Api.ArmaMissions.Services;

public sealed class SqmTextEditor(string text)
{
    private readonly List<(int Start, int End, string Replacement)> _edits = [];

    public string Text { get; } = text;
    public string NewLine { get; } = text.Contains("\r\n") ? "\r\n" : "\n";
    public bool HasEdits => _edits.Count > 0;

    public static string ReadFile(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));

    public static void WriteFile(string path, string text) => File.WriteAllBytes(path, Encoding.Latin1.GetBytes(text));

    public static string ToFileText(string value) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(value));

    public void Replace(int start, int end, string replacement) => _edits.Add((start, end, replacement));

    public void Rename(SqmClassNode node, string name)
    {
        if (!node.Name.Equals(name, StringComparison.Ordinal))
        {
            Replace(node.NameStart, node.NameEnd, name);
        }
    }

    public void SetValue(SqmPropertyNode property, string value)
    {
        if (property.RawValue != value)
        {
            Replace(property.ValueStart, property.ValueEnd, value);
        }
    }

    public void DeleteLines(int start, int end)
    {
        var (lineStart, lineEnd) = WholeLines(start, end);
        Replace(lineStart, lineEnd, "");
    }

    public void InsertLinesBefore(int position, IReadOnlyList<string> lines, string indent)
    {
        var lineStart = LineStart(position);
        var at = lineStart >= 0 ? lineStart : position;
        var prefix = lineStart >= 0 ? "" : NewLine;
        Replace(at, at, prefix + Render(lines, indent));
    }

    public string IndentAt(int position)
    {
        var lineStart = LineStart(position);
        return lineStart >= 0 ? Text[lineStart..position] : "";
    }

    public string Apply()
    {
        var ordered = _edits.OrderByDescending(x => x.Start).ThenByDescending(x => x.End).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].End > ordered[i - 1].Start)
            {
                throw new InvalidOperationException($"overlapping mission.sqm edits at offsets {ordered[i].Start} and {ordered[i - 1].Start}");
            }
        }

        var result = new StringBuilder(Text);
        foreach (var (start, end, replacement) in ordered)
        {
            result.Remove(start, end - start).Insert(start, replacement);
        }

        return result.ToString();
    }

    private string Render(IReadOnlyList<string> lines, string indent)
    {
        var result = new StringBuilder();
        var depth = 0;
        foreach (var line in lines)
        {
            if (line == "};")
            {
                depth--;
            }

            result.Append(indent).Append('\t', Math.Max(depth, 0)).Append(ToFileText(line)).Append(NewLine);
            if (line == "{")
            {
                depth++;
            }
        }

        return result.ToString();
    }

    private int LineStart(int position)
    {
        var index = position;
        while (index > 0 && Text[index - 1] is ' ' or '\t')
        {
            index--;
        }

        return index == 0 || Text[index - 1] == '\n' ? index : -1;
    }

    private (int Start, int End) WholeLines(int start, int end)
    {
        var lineStart = LineStart(start);
        var lineEnd = end;
        while (lineEnd < Text.Length && Text[lineEnd] is ' ' or '\t')
        {
            lineEnd++;
        }

        if (lineEnd < Text.Length && Text[lineEnd] == '\r')
        {
            lineEnd++;
        }

        var endsLine = lineEnd >= Text.Length || Text[lineEnd] == '\n';
        if (lineStart < 0 || !endsLine)
        {
            return (start, end);
        }

        return (lineStart, Math.Min(lineEnd + 1, Text.Length));
    }
}
