using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Mappers;

public interface IAccountMapper
{
    Account MapToAccount(DomainAccount domainAccount);
}

public class AccountMapper(IDisplayNameService displayNameService) : IAccountMapper
{
    public Account MapToAccount(DomainAccount domainAccount)
    {
        return new Account
        {
            Id = domainAccount.Id,
            DisplayName = displayNameService.GetDisplayName(domainAccount),
            Settings = domainAccount.Settings,
            MembershipState = domainAccount.MembershipState,
            RolePreferences = domainAccount.RolePreferences,
            ServiceRecord = domainAccount.ServiceRecord,
            Admin = domainAccount.Admin,
            Application = domainAccount.Application,
            ArmaExperience = domainAccount.ArmaExperience,
            Background = domainAccount.Background,
            DiscordId = domainAccount.DiscordId,
            Dob = domainAccount.Dob,
            Email = domainAccount.Email,
            Firstname = domainAccount.Firstname,
            Lastname = domainAccount.Lastname,
            MilitaryExperience = domainAccount.MilitaryExperience,
            Nation = domainAccount.Nation,
            Rank = domainAccount.Rank,
            Reference = domainAccount.Reference,
            RoleAssignment = domainAccount.RoleAssignment,
            Steamname = domainAccount.Steamname,
            TeamspeakIdentities = domainAccount.TeamspeakIdentities,
            UnitAssignment = domainAccount.UnitAssignment,
            UnitsExperience = domainAccount.UnitsExperience
        };
    }
}
