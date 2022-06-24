using System.Threading.Tasks;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Commands
{
    public interface IQualificationsUpdateCommand
    {
        Task ExecuteAsync(string accountId, Qualifications qualifications);
    }

    public class QualificationsUpdateCommand : IQualificationsUpdateCommand
    {
        private readonly IAccountContext _accountContext;
        private readonly ILogger _logger;

        public QualificationsUpdateCommand(IAccountContext accountContext, ILogger logger)
        {
            _accountContext = accountContext;
            _logger = logger;
        }

        public async Task ExecuteAsync(string accountId, Qualifications qualifications)
        {
            var account = _accountContext.GetSingle(accountId);

            if (account.Qualifications.Medic != qualifications.Medic)
            {
                await _accountContext.Update(accountId, x => x.Qualifications.Medic, qualifications.Medic);
                _logger.LogAudit($"Medic qualification for {accountId} was {(qualifications.Medic ? "enabled" : "disabled")}");
            }

            if (account.Qualifications.Engineer != qualifications.Engineer)
            {
                await _accountContext.Update(accountId, x => x.Qualifications.Engineer, qualifications.Engineer);
                _logger.LogAudit($"Engineer qualification for {accountId} was {(qualifications.Engineer ? "enabled" : "disabled")}");
            }
        }
    }
}
