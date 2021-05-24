using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Mappers
{
    public interface IAccountMapper
    {
        Account MapToAccount(DomainAccount domainAccount);
    }

    public class AccountMapper : IAccountMapper
    {
        private readonly IDisplayNameService _displayNameService;

        public AccountMapper(IDisplayNameService displayNameService)
        {
            _displayNameService = displayNameService;
        }

        public Account MapToAccount(DomainAccount domainAccount)
        {
            return new()
            {
                Id = domainAccount.Id,
                DisplayName = _displayNameService.GetDisplayName(domainAccount),
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
}
