using System.Collections.Generic;
using UKSFWebsite.Api.Interfaces.Integrations;

namespace UKSFWebsite.Api.Services.Integrations {
    public class TeamspeakMetricsService : ITeamspeakMetricsService {
        public float GetWeeklyParticipationTrend(HashSet<string> teamspeakIdentities) => 3;
    }
}
