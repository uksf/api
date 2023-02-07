using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Controllers;

[Route("accounts/{id}/application")]
[Permissions(Permissions.Confirmed)]
public class ApplicationsController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;

    public ApplicationsController(IAccountContext accountContext, INotificationsService notificationsService, IUksfLogger logger)
    {
        _accountContext = accountContext;
        _notificationsService = notificationsService;
        _logger = logger;
    }

    [HttpPost]
    public async Task Post([FromServices] ICreateApplicationCommand createApplicationCommand, [FromRoute] string id, [FromBody] Account account)
    {
        await createApplicationCommand.ExecuteAsync(id, account);
    }

    [HttpPut]
    public async Task Update([FromRoute] string id, [FromBody] Account account)
    {
        var domainAccount = _accountContext.GetSingle(id);
        await _accountContext.Update(
            id,
            Builders<DomainAccount>.Update.Set(x => x.ArmaExperience, account.ArmaExperience)
                                   .Set(x => x.UnitsExperience, account.UnitsExperience)
                                   .Set(x => x.Background, account.Background)
                                   .Set(x => x.MilitaryExperience, account.MilitaryExperience)
                                   .Set(x => x.RolePreferences, account.RolePreferences)
                                   .Set(x => x.Reference, account.Reference)
        );

        _notificationsService.Add(
            new()
            {
                Owner = domainAccount.Application.Recruiter,
                Icon = NotificationIcons.Application,
                Message = $"{domainAccount.Firstname} {domainAccount.Lastname} updated their application",
                Link = $"/recruitment/{id}"
            }
        );

        var difference = domainAccount.Changes(_accountContext.GetSingle(id));
        _logger.LogAudit($"Application updated for {id}: {difference}");
    }
}
