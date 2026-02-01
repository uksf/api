using MassTransit;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Modpack.WorkshopModProcessing;

public class WorkshopModInstanceState : SagaStateMachineInstance, ISagaVersion
{
    [BsonId]
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; }
    public string WorkshopModId { get; set; }
    public string Operation { get; set; } // Install, Update, Uninstall
    public List<string> SelectedPbos { get; set; }

    // Observability and fault tracking
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? FaultedState { get; set; }
    public DateTime? LastErrorAt { get; set; }
}
