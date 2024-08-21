using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Mappers;

public interface IAccountMapper
{
    Account MapToAccount(DomainAccount account);
}

public class AccountMapper(IDisplayNameService displayNameService) : IAccountMapper
{
    public Account MapToAccount(DomainAccount account)
    {
        return new Account
        {
            Id = account.Id,
            DisplayName = displayNameService.GetDisplayName(account),
            Settings = account.Settings,
            MembershipState = account.MembershipState,
            RolePreferences = account.RolePreferences,
            ServiceRecord = account.ServiceRecord,
            Admin = account.Admin,
            Application = account.Application,
            ArmaExperience = account.ArmaExperience,
            Background = account.Background,
            DiscordId = account.DiscordId,
            Dob = account.Dob,
            Email = account.Email,
            Firstname = account.Firstname,
            Lastname = account.Lastname,
            MilitaryExperience = account.MilitaryExperience,
            Nation = account.Nation,
            Rank = account.Rank,
            Reference = account.Reference,
            RoleAssignment = account.RoleAssignment,
            Steamname = account.Steamname,
            TeamspeakIdentities = account.TeamspeakIdentities,
            UnitAssignment = account.UnitAssignment,
            UnitsExperience = account.UnitsExperience
        };
    }
}
