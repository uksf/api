using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Commands;

public interface IUpdateAccountTrainingCommandHandler
{
    Task ExecuteAsync(UpdateAccountTrainingCommand command);
    Task ExecuteAsync(BulkUpdateAccountTrainingCommand command);
}

public record UpdateAccountTrainingCommand(string AccountId, List<string> TrainingIds);

public record BulkUpdateAccountTrainingCommand(List<string> AccountIds, List<string> TrainingIds, bool Remove);

public class UpdateAccountTrainingCommandHandler(IAccountContext accountContext, ITrainingsContext trainingsContext, IUksfLogger logger)
    : IUpdateAccountTrainingCommandHandler
{
    public async Task ExecuteAsync(UpdateAccountTrainingCommand command)
    {
        var account = accountContext.GetSingle(command.AccountId);
        await accountContext.Update(account.Id, x => x.Trainings, command.TrainingIds);

        var allTrainings = trainingsContext.Get().ToList();
        var before = allTrainings.Where(x => account.Trainings.Contains(x.Id)).Select(x => x.Name).ToList();
        var after = allTrainings.Where(x => command.TrainingIds.Contains(x.Id)).Select(x => x.Name).ToList();
        logger.LogAudit($"Training updated for {account.Id}: {before.Changes(after)}");
    }

    public async Task ExecuteAsync(BulkUpdateAccountTrainingCommand command)
    {
        var accounts = command.AccountIds.Distinct()
                              .Select(id => accountContext.GetSingle(id) ?? throw new BadRequestException($"Account {id} not found"))
                              .ToList();

        foreach (var account in accounts)
        {
            var updated = command.Remove ? account.Trainings.Except(command.TrainingIds).ToList() : account.Trainings.Union(command.TrainingIds).ToList();
            if (updated.Count != account.Trainings.Count)
            {
                await ExecuteAsync(new UpdateAccountTrainingCommand(account.Id, updated));
            }
        }
    }
}
