﻿using System.Collections.Generic;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakMetricsService : ITeamspeakMetricsService {
        public float GetWeeklyParticipationTrend(HashSet<double> teamspeakIdentities) => 3;
    }
}