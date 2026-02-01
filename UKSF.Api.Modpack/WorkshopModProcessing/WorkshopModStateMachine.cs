using MassTransit;

namespace UKSF.Api.Modpack.WorkshopModProcessing;

public class WorkshopModStateMachine : MassTransitStateMachine<WorkshopModInstanceState>
{
    public WorkshopModStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => InstallRequested, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId).SelectId(_ => Guid.NewGuid()));
        Event(() => UpdateRequested, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId).SelectId(_ => Guid.NewGuid()));
        Event(() => UninstallRequested, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId).SelectId(_ => Guid.NewGuid()));

        Event(() => InstallDownloadComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => UpdateDownloadComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Event(() => InstallCheckComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => UpdateCheckComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Event(() => InterventionResolved, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Event(() => InstallComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => UpdateComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => UninstallComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Event(() => CleanupComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => OperationFaulted, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Initially(
            When(InstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Install";
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(InstallingDownloading)
                .Publish(context => new WorkshopModInstallDownloadCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            InstallingDownloading,
            When(InstallDownloadComplete)
                .TransitionTo(InstallingChecking)
                .Publish(context => new WorkshopModInstallCheckCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            InstallingChecking,
            When(InstallCheckComplete)
                .IfElse(
                    context => context.Message.InterventionRequired,
                    binder => binder.TransitionTo(InstallingAwaitingIntervention),
                    binder => binder.TransitionTo(Installing)
                                    .Publish(context => new WorkshopModInstallInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
                )
        );

        During(
            InstallingAwaitingIntervention,
            When(InterventionResolved)
                .Then(context => { context.Saga.SelectedPbos = context.Message.SelectedPbos; })
                .TransitionTo(Installing)
                .Publish(context => new WorkshopModInstallInternalCommand
                    {
                        WorkshopModId = context.Saga.WorkshopModId, SelectedPbos = context.Saga.SelectedPbos
                    }
                )
        );

        During(
            Installing,
            When(InstallComplete).TransitionTo(Cleanup).Publish(context => new WorkshopModCleanupCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        Initially(
            When(UpdateRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Update";
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(UpdatingDownloading)
                .Publish(context => new WorkshopModUpdateDownloadCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            UpdatingDownloading,
            When(UpdateDownloadComplete)
                .TransitionTo(UpdatingChecking)
                .Publish(context => new WorkshopModUpdateCheckCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            UpdatingChecking,
            When(UpdateCheckComplete)
                .IfElse(
                    context => context.Message.InterventionRequired,
                    binder => binder.TransitionTo(UpdatingAwaitingIntervention),
                    binder => binder.TransitionTo(Updating)
                                    .Publish(context => new WorkshopModUpdateInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
                )
        );

        During(
            UpdatingAwaitingIntervention,
            When(InterventionResolved)
                .Then(context => { context.Saga.SelectedPbos = context.Message.SelectedPbos; })
                .TransitionTo(Updating)
                .Publish(context => new WorkshopModUpdateInternalCommand
                    {
                        WorkshopModId = context.Saga.WorkshopModId, SelectedPbos = context.Saga.SelectedPbos
                    }
                )
        );

        During(
            Updating,
            When(UpdateComplete).TransitionTo(Cleanup).Publish(context => new WorkshopModCleanupCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        Initially(
            When(UninstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Uninstall";
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Uninstalling)
                .Publish(context => new WorkshopModUninstallInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            Uninstalling,
            When(UninstallComplete).TransitionTo(Cleanup).Publish(context => new WorkshopModCleanupCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            InstallingAwaitingIntervention,
            When(UninstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Uninstall";
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Uninstalling)
                .Publish(context => new WorkshopModUninstallInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(Cleanup, When(CleanupComplete).Then(context => context.Saga.CompletedAt = DateTime.UtcNow).Finalize());

        // Handle faults in any state except final states
        DuringAny(
            When(OperationFaulted)
                .Then(context =>
                    {
                        context.Saga.LastErrorMessage = context.Message.ErrorMessage;
                        context.Saga.FaultedState = context.Message.FaultedState;
                        context.Saga.LastErrorAt = context.Message.FaultedAt;
                        context.Saga.CompletedAt = DateTime.UtcNow;
                    }
                )
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }

    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
    // ReSharper disable MemberCanBePrivate.Global
    public State InstallingDownloading { get; private set; } = null!;
    public State InstallingChecking { get; private set; } = null!;
    public State InstallingAwaitingIntervention { get; private set; } = null!;
    public State Installing { get; private set; } = null!;

    public State UpdatingDownloading { get; private set; } = null!;
    public State UpdatingChecking { get; private set; } = null!;
    public State UpdatingAwaitingIntervention { get; private set; } = null!;
    public State Updating { get; private set; } = null!;

    public State Uninstalling { get; private set; } = null!;
    public State Cleanup { get; private set; } = null!;

    public Event<WorkshopModInstallCommand> InstallRequested { get; private set; } = null!;
    public Event<WorkshopModUpdateCommand> UpdateRequested { get; private set; } = null!;
    public Event<WorkshopModUninstallCommand> UninstallRequested { get; private set; } = null!;

    public Event<WorkshopModInstallDownloadComplete> InstallDownloadComplete { get; private set; } = null!;
    public Event<WorkshopModUpdateDownloadComplete> UpdateDownloadComplete { get; private set; } = null!;

    public Event<WorkshopModInstallCheckComplete> InstallCheckComplete { get; private set; } = null!;
    public Event<WorkshopModUpdateCheckComplete> UpdateCheckComplete { get; private set; } = null!;

    public Event<WorkshopModInterventionResolved> InterventionResolved { get; private set; } = null!;

    public Event<WorkshopModInstallComplete> InstallComplete { get; private set; } = null!;
    public Event<WorkshopModUpdateComplete> UpdateComplete { get; private set; } = null!;
    public Event<WorkshopModUninstallComplete> UninstallComplete { get; private set; } = null!;

    public Event<WorkshopModCleanupComplete> CleanupComplete { get; private set; } = null!;

    public Event<WorkshopModOperationFaulted> OperationFaulted { get; private set; } = null!;
    // ReSharper restore MemberCanBePrivate.Global
    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local
}
