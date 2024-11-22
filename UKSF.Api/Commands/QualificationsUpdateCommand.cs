using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Commands;

public interface IQualificationsUpdateCommand
{
    Task ExecuteAsync(string accountId, Qualifications qualifications);
}

public class QualificationsUpdateCommand(IAccountContext accountContext, IUksfLogger logger) : IQualificationsUpdateCommand
{
    public async Task ExecuteAsync(string accountId, Qualifications qualifications)
    {
        var account = accountContext.GetSingle(accountId);

        if (account.Qualifications.Medic != qualifications.Medic)
        {
            await accountContext.Update(accountId, x => x.Qualifications.Medic, qualifications.Medic);
            logger.LogAudit($"Medic qualification for {accountId} was {(qualifications.Medic ? "enabled" : "disabled")}");
        }

        if (account.Qualifications.Engineer != qualifications.Engineer)
        {
            await accountContext.Update(accountId, x => x.Qualifications.Engineer, qualifications.Engineer);
            logger.LogAudit($"Engineer qualification for {accountId} was {(qualifications.Engineer ? "enabled" : "disabled")}");
        }
    }
}
