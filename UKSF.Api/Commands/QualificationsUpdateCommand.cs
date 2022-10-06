using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Commands;

public interface IQualificationsUpdateCommand
{
    Task ExecuteAsync(string accountId, Qualifications qualifications);
}

public class QualificationsUpdateCommand : IQualificationsUpdateCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IUksfLogger _logger;

    public QualificationsUpdateCommand(IAccountContext accountContext, IUksfLogger logger)
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
