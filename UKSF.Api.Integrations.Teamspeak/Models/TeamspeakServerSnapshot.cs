namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeamspeakServerSnapshot
{
    public DateTime Timestamp { get; set; }
    public HashSet<TeamspeakClient> Users { get; set; }
}
