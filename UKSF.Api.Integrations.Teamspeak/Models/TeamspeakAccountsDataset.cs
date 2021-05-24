using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Models
{
    public class TeamspeakAccountsDataset
    {
        public List<TeamspeakAccountDataset> Commanders;
        public List<TeamspeakAccountDataset> Guests;
        public List<TeamspeakAccountDataset> Members;
        public List<TeamspeakAccountDataset> Recruiters;
    }

    public class TeamspeakAccountDataset
    {
        public string DisplayName;
    }
}
