namespace UKSF.Api.Teamspeak.Models
{
    public class TeamspeakClient
    {
        public int ChannelId;
        public string ChannelName;
        public int ClientDbId;
        public string ClientName;
    }

    public class TeamspeakConnectClient
    {
        public int ClientDbId;
        public string ClientName;
        public bool Connected;
    }
}
