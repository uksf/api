namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeamspeakAccountsDataset
{
    public List<TeamspeakAccountDataset> Commanders { get; set; }
    public List<TeamspeakAccountDataset> Guests { get; set; }
    public List<TeamspeakAccountDataset> Members { get; set; }
    public List<TeamspeakAccountDataset> Recruiters { get; set; }
}

public class TeamspeakAccountDataset
{
    public string DisplayName { get; set; }
}
