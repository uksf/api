namespace UKSF.Api.ArmaServer.Models.Persistence;

public class ChunkEnvelope
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Index { get; set; }
    public int Total { get; set; }
    public string Data { get; set; } = string.Empty;
}
