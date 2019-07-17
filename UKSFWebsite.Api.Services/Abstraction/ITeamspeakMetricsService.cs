using System.Collections.Generic;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ITeamspeakMetricsService {
        float GetWeeklyParticipationTrend(HashSet<string> teamspeakIdentities);
    }
}
