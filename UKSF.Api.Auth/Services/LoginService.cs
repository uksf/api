using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using UKSF.Api.Auth.Exceptions;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Auth.Services
{
    public interface ILoginService
    {
        string Login(string email, string password);
        string LoginForPasswordReset(string email);
        string RegenerateBearerToken(string accountId);
    }

    public class LoginService : ILoginService
    {
        private readonly IAccountContext _accountContext;
        private readonly IPermissionsService _permissionsService;

        public LoginService(IAccountContext accountContext, IPermissionsService permissionsService)
        {
            _accountContext = accountContext;
            _permissionsService = permissionsService;
        }

        public string Login(string email, string password)
        {
            DomainAccount domainAccount = AuthenticateAccount(email, password);
            return GenerateBearerToken(domainAccount);
        }

        public string LoginForPasswordReset(string email)
        {
            DomainAccount domainAccount = AuthenticateAccount(email, "", true);
            return GenerateBearerToken(domainAccount);
        }

        public string RegenerateBearerToken(string accountId)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(accountId);
            if (domainAccount == null)
            {
                throw new BadRequestException("No user found with that email");
            }

            return GenerateBearerToken(domainAccount);
        }

        private DomainAccount AuthenticateAccount(string email, string password, bool passwordReset = false)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(x => string.Equals(x.Email, email, StringComparison.InvariantCultureIgnoreCase));
            if (domainAccount == null)
            {
                throw new BadRequestException("No user found with that email");
            }

            if (passwordReset)
            {
                return domainAccount;
            }

            if (!BCrypt.Net.BCrypt.Verify(password, domainAccount.Password))
            {
                throw new BadRequestException("Incorrect password");
            }

            return domainAccount;
        }

        private string GenerateBearerToken(DomainAccount domainAccount)
        {
            List<Claim> claims = new() { new(ClaimTypes.Email, domainAccount.Email, ClaimValueTypes.String), new(ClaimTypes.Sid, domainAccount.Id, ClaimValueTypes.String) };
            claims.AddRange(_permissionsService.GrantPermissions(domainAccount).Select(x => new Claim(ClaimTypes.Role, x)));

            return JsonConvert.ToString(
                new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(
                        ApiAuthExtensions.TokenIssuer,
                        ApiAuthExtensions.TokenAudience,
                        claims,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddDays(15),
                        new(ApiAuthExtensions.SecurityKey, SecurityAlgorithms.HmacSha256)
                    )
                )
            );
        }
    }
}
