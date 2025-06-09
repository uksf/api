using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("accounts/{id}/application")]
[Permissions(Permissions.Confirmed)]
public class ApplicationsController(IAccountContext accountContext, INotificationsService notificationsService, IUksfLogger logger) : ControllerBase
{
    [HttpPost]
    public async Task Post([FromServices] ICreateApplicationCommand createApplicationCommand, [FromRoute] string id, [FromBody] Account account)
    {
        await createApplicationCommand.ExecuteAsync(id, account);
    }

    [HttpPut]
    public async Task Update([FromRoute] string id, [FromBody] Account account)
    {
        var oldAccount = accountContext.GetSingle(id);
        await accountContext.Update(
            id,
            Builders<DomainAccount>.Update.Set(x => x.ArmaExperience, account.ArmaExperience)
                                   .Set(x => x.UnitsExperience, account.UnitsExperience)
                                   .Set(x => x.Background, account.Background)
                                   .Set(x => x.MilitaryExperience, account.MilitaryExperience)
                                   .Set(x => x.RolePreferences, account.RolePreferences)
                                   .Set(x => x.Reference, account.Reference)
        );

        notificationsService.Add(
            new DomainNotification
            {
                Owner = oldAccount.Application.Recruiter,
                Icon = NotificationIcons.Application,
                Message = $"{oldAccount.Firstname} {oldAccount.Lastname} updated their application",
                Link = $"/recruitment/{id}"
            }
        );

        var difference = oldAccount.Changes(accountContext.GetSingle(id));
        logger.LogAudit($"Application updated for {id}: {difference}");
    }
}
