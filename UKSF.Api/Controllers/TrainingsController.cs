using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Controllers;

[Permissions(Permissions.Member)]
[Route("trainings")]
public class TrainingsController(ITrainingsContext trainingsContext, IAccountContext accountContext, IUksfLogger logger) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public IEnumerable<DomainTraining> GetTrainings()
    {
        return trainingsContext.Get();
    }

    [Authorize]
    [HttpGet("check-unique")]
    public bool CheckUnique([FromQuery] string check, [FromQuery] string ignoreId = null)
    {
        if (string.IsNullOrEmpty(check))
        {
            return true;
        }

        var existingTraining = trainingsContext.GetSingle(x => (string.IsNullOrEmpty(ignoreId) || x.Id != ignoreId) &&
                                                               (x.Name == check || x.ShortName == check || x.TeamspeakGroup == check)
        );
        return existingTraining is null;
    }

    [Permissions(Permissions.Command)]
    [HttpPost]
    public async Task AddTraining([FromBody] DomainTraining training)
    {
        await trainingsContext.Add(training);
        logger.LogAudit($"Training added '{training.Name}, {training.ShortName}, {training.TeamspeakGroup}'");
    }

    [Permissions(Permissions.Command)]
    [HttpPatch]
    public async Task<IEnumerable<DomainTraining>> EditTraining([FromBody] DomainTraining training)
    {
        var oldTraining = trainingsContext.GetSingle(x => x.Id == training.Id);
        logger.LogAudit(
            $"Training updated from '{oldTraining.Name}, {oldTraining.ShortName}, {oldTraining.TeamspeakGroup}' to '{training.Name}, {training.ShortName}, {training.TeamspeakGroup}'"
        );
        await trainingsContext.Update(
            training.Id,
            Builders<DomainTraining>.Update.Set(x => x.Name, training.Name)
                                    .Set(x => x.ShortName, training.ShortName)
                                    .Set(x => x.TeamspeakGroup, training.TeamspeakGroup)
        );

        return trainingsContext.Get();
    }

    [Permissions(Permissions.Command)]
    [HttpDelete("{trainingId}")]
    public async Task<IEnumerable<DomainTraining>> DeleteTraining([FromRoute] string trainingId)
    {
        var training = trainingsContext.GetSingle(x => x.Id == trainingId);
        logger.LogAudit($"Training deleted '{training.Name}'");
        await trainingsContext.Delete(trainingId);
        await accountContext.UpdateMany(x => x.Trainings.Contains(training.Id), Builders<DomainAccount>.Update.Pull(x => x.Trainings, training.Id));

        return trainingsContext.Get();
    }
}
