using System.Collections.Generic;
using UKSF.Api.Interfaces.Integrations.Teamspeak;

namespace UKSF.Api.Services.Integrations.Teamspeak {
    public class TeamspeakMetricsService : ITeamspeakMetricsService {
        public float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities) => 3;
    }
}
