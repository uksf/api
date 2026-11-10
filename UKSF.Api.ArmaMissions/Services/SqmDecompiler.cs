namespace UKSF.Api.ArmaMissions.Services;

public class SqmDecompiler : ISqmDecompiler
{
    public async Task<bool> IsBinarized(string sqmPath)
    {
        await using var stream = File.OpenRead(sqmPath);
        var header = new byte[4];
        var read = await stream.ReadAtLeastAsync(header, header.Length, false);
        return RapDecoder.IsRapified(header.AsSpan(0, read));
    }

    public async Task Decompile(string sqmPath)
    {
        var text = RapDecoder.ToText(await File.ReadAllBytesAsync(sqmPath));
        await File.WriteAllTextAsync(sqmPath, text);
    }
}
