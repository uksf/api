using System;
using UKSFWebsite.Api.Interfaces.Integrations;

namespace UKSFWebsite.Api.Services.Integrations.Procedures {
    public class SendClientsUpdate : ITeamspeakProcedure {
        private readonly ITeamspeakService teamspeakService;

        public SendClientsUpdate(ITeamspeakService teamspeakService) => this.teamspeakService = teamspeakService;

        public void Run(string[] args) {
            string clientsJson = args[0];
            Console.WriteLine($"Got data for online clients: {clientsJson}");
            if (string.IsNullOrEmpty(clientsJson)) return;
            Console.WriteLine("Updating online clients");
            teamspeakService.UpdateClients(clientsJson).Wait();
        }
    }
}
