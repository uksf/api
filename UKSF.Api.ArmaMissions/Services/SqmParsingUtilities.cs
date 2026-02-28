namespace UKSF.Api.ArmaMissions.Services;

public static class SqmParsingUtilities
{
    public static int GetIndexByKey(List<string> source, string key)
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

    public static List<string> ReadBlock(List<string> source, ref int index)
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

    public static List<string> ReadBlock(List<string> source, int startIndex)
    {
        var index = startIndex;
        return ReadBlock(source, ref index);
    }

    public static List<string> ReadBlockByKey(List<string> source, string key)
    {
        var index = GetIndexByKey(source, key);
        return index == -1 ? [] : ReadBlock(source, ref index);
    }
}
