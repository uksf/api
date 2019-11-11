using System;

namespace UKSFWebsite.Api.Services.Integrations.Procedures {
    public class Pong : ITeamspeakProcedure {
        public void Run(string[] args) {
            PipeManager.PongTime = DateTime.Now;
        }
    }
}
