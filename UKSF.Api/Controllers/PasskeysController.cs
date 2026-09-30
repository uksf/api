using System.Buffers.Text;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Authorize]
public class PasskeysController(IPasskeyContext passkeyContext, IPasskeyService passkeyService, IAccountService accountService, IUksfLogger logger)
    : ControllerBase
{
    // Serialises the last-passkey check with the delete, so parallel deletes cannot remove every sign-in method
    private static readonly SemaphoreSlim DeleteLock = new(1, 1);

    [HttpGet]
    public PasskeysResponse Get()
    {
        var account = accountService.GetUserAccount();
        return new PasskeysResponse
        {
            HasPassword = !string.IsNullOrEmpty(account.Password),
            Passkeys = passkeyContext.Get(x => x.AccountId == account.Id).OrderBy(x => x.Created).Select(MapPasskey).ToList()
        };
    }

    [HttpPost("options")]
    public PasskeyOptionsResponse<CredentialCreateOptions> RegistrationOptions()
    {
        var account = accountService.GetUserAccount();
        return passkeyService.CreateRegistrationOptions(account.Id, account.Email, $"{account.Firstname} {account.Lastname}");
    }

    // For the browser's automatic passkey upgrade after a password sign-in (conditional create)
    [HttpPost("options/automatic")]
    public PasskeyOptionsResponse<CredentialCreateOptions> AutomaticRegistrationOptions()
    {
        var account = accountService.GetUserAccount();
        return passkeyService.CreateRegistrationOptions(account.Id, account.Email, $"{account.Firstname} {account.Lastname}", automatic: true);
    }

    [HttpPost]
    public async Task<PasskeyResponse> Register([FromBody] PasskeyRegistrationRequest request)
    {
        var account = accountService.GetUserAccount();
        var passkey = await passkeyService.VerifyRegistration(request.FlowId, account.Email, request.Credential);
        if (passkey.AccountId != account.Id)
        {
            throw new BadRequestException("Passkey registration does not match this account, please try again");
        }

        await passkeyContext.Add(passkey);
        logger.LogAudit($"Passkey '{passkey.Name}' added for {account.Id}");
        return MapPasskey(passkey);
    }

    [HttpDelete("{id}")]
    public async Task Delete([FromRoute] string id)
    {
        var account = accountService.GetUserAccount();
        await DeleteLock.WaitAsync();
        try
        {
            var passkeys = passkeyContext.Get(x => x.AccountId == account.Id).ToList();
            var passkey = passkeys.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("Passkey not found");
            if (passkeys.Count == 1 && string.IsNullOrEmpty(account.Password))
            {
                throw new BadRequestException("Set a password before removing your only passkey");
            }

            await passkeyContext.Delete(passkey);
            logger.LogAudit($"Passkey '{passkey.Name}' removed for {account.Id}");
        }
        finally
        {
            DeleteLock.Release();
        }
    }

    private static PasskeyResponse MapPasskey(DomainPasskey passkey)
    {
        return new PasskeyResponse
        {
            Id = passkey.Id,
            CredentialId = Base64Url.EncodeToString(passkey.CredentialId),
            Name = passkey.Name,
            Created = passkey.Created,
            LastUsed = passkey.LastUsed,
            IsBackedUp = passkey.IsBackedUp
        };
    }
}
