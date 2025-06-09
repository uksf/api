using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Commands;

public interface IUpdateAccountTrainingCommandHandler
{
    Task ExecuteAsync(UpdateAccountTrainingCommand command);
}

public record UpdateAccountTrainingCommand(string AccountId, List<string> TrainingIds);

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
}
