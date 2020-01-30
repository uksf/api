using System.Collections.Generic;

namespace UKSF.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakMetricsService {
        float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities);
    }
}
