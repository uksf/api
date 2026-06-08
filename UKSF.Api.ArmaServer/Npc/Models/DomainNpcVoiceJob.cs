using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Npc.Models;

public enum NpcMoodStatus
{
    Pending,
    Generating,
    Ready,
    Failed
}

public class NpcMoodTask
{
    [JsonPropertyName("mood")]
    public string Mood { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public NpcMoodStatus Status { get; set; } = NpcMoodStatus.Pending;

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

public class DomainNpcVoiceJob : MongoObject
{
    [JsonPropertyName("baseVoiceId")]
    public string BaseVoiceId { get; set; } = string.Empty;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("moods")]
    public List<NpcMoodTask> Moods { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static DomainNpcVoiceJob NewJob(string baseVoiceId, string ownerId) =>
        new()
        {
            BaseVoiceId = baseVoiceId,
            OwnerId = ownerId,
            Moods = MoodScripts.Generated.Select(m => new NpcMoodTask { Mood = m, Status = NpcMoodStatus.Pending }).ToList()
        };
}
