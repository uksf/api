namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeamspeakClient
{
    public int ChannelId { get; set; }
    public string ChannelName { get; set; }
    public int ClientDbId { get; set; }
    public string ClientName { get; set; }
}

public class TeamspeakConnectClient
{
    public int ClientDbId { get; set; }
    public string ClientName { get; set; }
    public bool Connected { get; set; }
}
