using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services;

public interface IAccountService
{
    DomainAccount GetUserAccount();
}

public class AccountService : IAccountService
{
    private readonly IAccountContext _accountContext;
    private readonly IHttpContextService _httpContextService;

    public AccountService(IAccountContext accountContext, IHttpContextService httpContextService)
    {
        _accountContext = accountContext;
        _httpContextService = httpContextService;
    }

    public DomainAccount GetUserAccount()
    {
        return _accountContext.GetSingle(_httpContextService.GetUserId());
    }
}
