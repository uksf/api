using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

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
