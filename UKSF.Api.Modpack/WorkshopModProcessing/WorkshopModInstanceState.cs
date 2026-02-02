using MassTransit;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Modpack.WorkshopModProcessing;

#nullable enable

public class WorkshopModInstanceState : SagaStateMachineInstance, ISagaVersion
{
    [BsonId]
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; } = string.Empty;
    public string WorkshopModId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // Install, Update, Uninstall
    public List<string> SelectedPbos { get; set; } = [];

    // Observability and fault tracking
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? FaultedState { get; set; }
    public DateTime? LastErrorAt { get; set; }
}
