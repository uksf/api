using System.Collections.Generic;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Teamspeak {
    public class TeamspeakMetricsService : ITeamspeakMetricsService {
        public float GetWeeklyParticipationTrend(HashSet<string> teamspeakIdentities) => 3;
    }
}
