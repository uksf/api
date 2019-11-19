using System.Collections.Generic;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakMetricsService {
        float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities);
    }
}
