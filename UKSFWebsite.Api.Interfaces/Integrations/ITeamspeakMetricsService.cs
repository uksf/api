using System.Collections.Generic;

namespace UKSFWebsite.Api.Interfaces.Integrations {
    public interface ITeamspeakMetricsService {
        float GetWeeklyParticipationTrend(HashSet<string> teamspeakIdentities);
    }
}
