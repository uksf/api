namespace UKSF.Api.ArmaMissions.Services;

public static class PboLzss
{
    private const int MaxDistance = 0x0FFF;
    private const int MinLength = 3;
    private const int MaxLength = 18;
    private const int MaxCandidates = 64;

    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream(input.Length / 2 + 16);
        Dictionary<int, List<int>> positionsByPrefix = [];
        var group = new byte[1 + 8 * 2];
        var position = 0;
        while (position < input.Length)
        {
            byte flags = 0;
            var groupLength = 1;
            for (var bit = 0; bit < 8 && position < input.Length; bit++)
            {
                var (distance, length) = LongestMatch(input, position, positionsByPrefix);
                var step = length >= MinLength ? length : 1;
                if (length >= MinLength)
                {
                    group[groupLength++] = (byte)distance;
                    group[groupLength++] = (byte)(((distance >> 4) & 0xF0) | (length - MinLength));
                }
                else
                {
                    flags |= (byte)(1 << bit);
                    group[groupLength++] = input[position];
                }

                for (var i = 0; i < step; i++)
                {
                    Remember(input, position + i, positionsByPrefix);
                }

                position += step;
            }

            group[0] = flags;
            output.Write(group, 0, groupLength);
        }

        uint checksum = 0;
        foreach (var value in input)
        {
            checksum += value;
        }

        output.Write(BitConverter.GetBytes(checksum));
        return output.ToArray();
    }

    private static (int Distance, int Length) LongestMatch(ReadOnlySpan<byte> input, int position, Dictionary<int, List<int>> positionsByPrefix)
    {
        if (position + MinLength > input.Length || !positionsByPrefix.TryGetValue(Prefix(input, position), out var candidates))
        {
            return (0, 0);
        }

        var limit = Math.Min(MaxLength, input.Length - position);
        var best = (Distance: 0, Length: 0);
        for (var i = candidates.Count - 1; i >= 0 && candidates.Count - i <= MaxCandidates; i--)
        {
            var distance = position - candidates[i];
            if (distance > MaxDistance)
            {
                break;
            }

            var length = 0;
            while (length < limit && input[candidates[i] + length] == input[position + length])
            {
                length++;
            }

            if (length > best.Length)
            {
                best = (distance, length);
                if (length == limit)
                {
                    break;
                }
            }
        }

        return best;
    }

    private static void Remember(ReadOnlySpan<byte> input, int position, Dictionary<int, List<int>> positionsByPrefix)
    {
        if (position + MinLength > input.Length)
        {
            return;
        }

        var prefix = Prefix(input, position);
        if (!positionsByPrefix.TryGetValue(prefix, out var positions))
        {
            positionsByPrefix[prefix] = positions = [];
        }

        if (positions.Count >= MaxCandidates * 2)
        {
            positions.RemoveRange(0, positions.Count - MaxCandidates);
        }

        positions.Add(position);
    }

    private static int Prefix(ReadOnlySpan<byte> input, int position) => input[position] | (input[position + 1] << 8) | (input[position + 2] << 16);
}
