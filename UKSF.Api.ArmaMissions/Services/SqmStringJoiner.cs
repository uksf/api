using System.Text;

namespace UKSF.Api.ArmaMissions.Services;

public static class SqmStringJoiner
{
    public static string Join(string line)
    {
        var result = new StringBuilder(line.Length);
        var index = 0;
        while (index < line.Length)
        {
            if (line[index] != '"')
            {
                result.Append(line[index++]);
                continue;
            }

            result.Append('"');
            index++;
            while (true)
            {
                index = AppendContent(line, index, result);
                if (index >= line.Length)
                {
                    return result.ToString();
                }

                var continuation = ContinuationStart(line, index + 1);
                if (continuation < 0)
                {
                    result.Append('"');
                    index++;
                    break;
                }

                index = continuation + 1;
            }
        }

        return result.ToString();
    }

    private static int AppendContent(string line, int index, StringBuilder result)
    {
        while (index < line.Length)
        {
            if (line[index] == '"')
            {
                if (index + 1 >= line.Length || line[index + 1] != '"')
                {
                    return index;
                }

                result.Append("\"\"");
                index += 2;
                continue;
            }

            result.Append(line[index++]);
        }

        return index;
    }

    private static int ContinuationStart(string line, int index)
    {
        index = SkipWhitespace(line, index);
        if (index + 1 >= line.Length || line[index] != '\\' || line[index + 1] != 'n')
        {
            return -1;
        }

        index = SkipWhitespace(line, index + 2);
        return index < line.Length && line[index] == '"' ? index : -1;
    }

    private static int SkipWhitespace(string line, int index)
    {
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        return index;
    }
}
