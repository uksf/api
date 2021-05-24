using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Services
{
    public interface ITeamspeakMetricsService
    {
        float GetWeeklyParticipationTrend(HashSet<int> teamspeakIdentities);
    }

    public class TeamspeakMetricsService : ITeamspeakMetricsService
    {
        public float GetWeeklyParticipationTrend(HashSet<int> teamspeakIdentities)
        {
            return 3;
        }
    }
}
