using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Services {
    public interface ITeamspeakMetricsService {
        float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities);
    }

    public class TeamspeakMetricsService : ITeamspeakMetricsService {
        public float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities) => 3;
    }
}
