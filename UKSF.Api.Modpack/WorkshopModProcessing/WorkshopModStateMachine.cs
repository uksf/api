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

        Event(() => DownloadComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => CheckComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => InterventionResolved, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => ExecuteComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => UninstallComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => CleanupComplete, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));
        Event(() => OperationFaulted, x => x.CorrelateBy(s => s.WorkshopModId, context => context.Message.WorkshopModId));

        Initially(
            When(InstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Install";
                        context.Saga.OperationType = WorkshopModOperationType.Install;
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Downloading)
                .Publish(context => new WorkshopModDownloadCommand
                             {
                                 WorkshopModId = context.Saga.WorkshopModId, OperationType = WorkshopModOperationType.Install
                             }
                )
        );

        Initially(
            When(UpdateRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Update";
                        context.Saga.OperationType = WorkshopModOperationType.Update;
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Downloading)
                .Publish(context => new WorkshopModDownloadCommand
                             {
                                 WorkshopModId = context.Saga.WorkshopModId, OperationType = WorkshopModOperationType.Update
                             }
                )
        );

        During(
            Downloading,
            When(DownloadComplete)
                .TransitionTo(Checking)
                .Publish(context => new WorkshopModCheckCommand
                    {
                        WorkshopModId = context.Saga.WorkshopModId, OperationType = context.Saga.OperationType!.Value
                    }
                )
        );

        During(
            Checking,
            When(CheckComplete)
                .IfElse(
                    context => context.Message.InterventionRequired,
                    binder => binder.TransitionTo(AwaitingIntervention),
                    binder => binder.Then(context => { context.Saga.SelectedPbos = context.Message.AvailablePbos ?? context.Saga.SelectedPbos; })
                                    .TransitionTo(Executing)
                                    .Publish(context => new WorkshopModExecuteCommand
                                        {
                                            WorkshopModId = context.Saga.WorkshopModId,
                                            OperationType = context.Saga.OperationType!.Value,
                                            SelectedPbos = context.Saga.SelectedPbos
                                        }
                                    )
                )
        );

        During(
            AwaitingIntervention,
            When(InterventionResolved)
                .Then(context => { context.Saga.SelectedPbos = context.Message.SelectedPbos; })
                .TransitionTo(Executing)
                .Publish(context => new WorkshopModExecuteCommand
                    {
                        WorkshopModId = context.Saga.WorkshopModId,
                        OperationType = context.Saga.OperationType!.Value,
                        SelectedPbos = context.Saga.SelectedPbos
                    }
                )
        );

        During(
            AwaitingIntervention,
            When(UninstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Uninstall";
                        context.Saga.OperationType = null;
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Uninstalling)
                .Publish(context => new WorkshopModUninstallInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            Executing,
            When(ExecuteComplete)
                .Then(context => context.Saga.FilesChanged = context.Message.FilesChanged)
                .TransitionTo(Cleanup)
                .Publish(context => new WorkshopModCleanupCommand { WorkshopModId = context.Saga.WorkshopModId, FilesChanged = context.Saga.FilesChanged })
        );

        Initially(
            When(UninstallRequested)
                .Then(context =>
                    {
                        context.Saga.WorkshopModId = context.Message.WorkshopModId;
                        context.Saga.Operation = "Uninstall";
                        context.Saga.OperationType = null;
                        context.Saga.StartedAt = DateTime.UtcNow;
                    }
                )
                .TransitionTo(Uninstalling)
                .Publish(context => new WorkshopModUninstallInternalCommand { WorkshopModId = context.Saga.WorkshopModId })
        );

        During(
            Uninstalling,
            When(UninstallComplete)
                .Then(context => context.Saga.FilesChanged = context.Message.FilesChanged)
                .TransitionTo(Cleanup)
                .Publish(context => new WorkshopModCleanupCommand { WorkshopModId = context.Saga.WorkshopModId, FilesChanged = context.Saga.FilesChanged })
        );

        During(Cleanup, When(CleanupComplete).Then(context => context.Saga.CompletedAt = DateTime.UtcNow).Finalize());

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
    public State Downloading { get; private set; } = null!;
    public State Checking { get; private set; } = null!;
    public State AwaitingIntervention { get; private set; } = null!;
    public State Executing { get; private set; } = null!;
    public State Uninstalling { get; private set; } = null!;
    public State Cleanup { get; private set; } = null!;

    public Event<WorkshopModInstallCommand> InstallRequested { get; private set; } = null!;
    public Event<WorkshopModUpdateCommand> UpdateRequested { get; private set; } = null!;
    public Event<WorkshopModUninstallCommand> UninstallRequested { get; private set; } = null!;

    public Event<WorkshopModDownloadComplete> DownloadComplete { get; private set; } = null!;
    public Event<WorkshopModCheckComplete> CheckComplete { get; private set; } = null!;
    public Event<WorkshopModInterventionResolved> InterventionResolved { get; private set; } = null!;
    public Event<WorkshopModExecuteComplete> ExecuteComplete { get; private set; } = null!;
    public Event<WorkshopModUninstallComplete> UninstallComplete { get; private set; } = null!;
    public Event<WorkshopModCleanupComplete> CleanupComplete { get; private set; } = null!;

    public Event<WorkshopModOperationFaulted> OperationFaulted { get; private set; } = null!;
    // ReSharper restore MemberCanBePrivate.Global
    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local
}
