namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakManager {
        void Start();
        void SendProcedure(string procedure);
    }
}
