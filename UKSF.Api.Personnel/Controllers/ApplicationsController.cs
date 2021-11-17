using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Personnel.Commands;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;

        public ApplicationsController(
            IAccountContext accountContext,
            IAccountService accountService,
            INotificationsService notificationsService,
            ILogger logger
        )
        {
            _accountContext = accountContext;
            _accountService = accountService;
            _notificationsService = notificationsService;
            _logger = logger;
        }

        [HttpPost, Permissions(Permissions.CONFIRMED)]
        public async Task Post([FromServices] ICreateApplicationCommand createApplicationCommand, [FromBody] Account account)
        {
            await createApplicationCommand.ExecuteAsync(account);
        }

        [HttpPost("update"), Permissions(Permissions.CONFIRMED)]
        public async Task Update([FromBody] Account account)
        {
            var domainAccount = _accountService.GetUserAccount();
            await _accountContext.Update(
                domainAccount.Id,
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
                    Icon = NotificationIcons.APPLICATION,
                    Message = $"{domainAccount.Firstname} {domainAccount.Lastname} updated their application",
                    Link = $"/recruitment/{domainAccount.Id}"
                }
            );

            var difference = domainAccount.Changes(_accountContext.GetSingle(domainAccount.Id));
            _logger.LogAudit($"Application updated for {domainAccount.Id}: {difference}");
        }
    }
}
