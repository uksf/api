using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Mappers;

public interface ICompletedApplicationMapper
{
    CompletedApplication MapToCompletedApplication(DomainAccount account);
}

public class CompletedApplicationMapper(IAccountMapper accountMapper, IDisplayNameService displayNameService) : ICompletedApplicationMapper
{
    public CompletedApplication MapToCompletedApplication(DomainAccount account)
    {
        return new CompletedApplication
        {
            Account = accountMapper.MapToAccount(account),
            DisplayName = displayNameService.GetDisplayNameWithoutRank(account),
            DaysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
            Recruiter = displayNameService.GetDisplayName(account.Application.Recruiter)
        };
    }
}
