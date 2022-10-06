using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("accounts/nations")]
public class AccountNationsController : ControllerBase
{
    private readonly IAllNationsByAccountQuery _allNationsByAccountQuery;

    public AccountNationsController(IAllNationsByAccountQuery allNationsByAccountQuery)
    {
        _allNationsByAccountQuery = allNationsByAccountQuery;
    }

    [HttpGet]
    public async Task<List<string>> GetNations()
    {
        return await _allNationsByAccountQuery.ExecuteAsync();
    }
}
