using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("accounts/nations")]
public class AccountNationsController(IAllNationsByAccountQuery allNationsByAccountQuery) : ControllerBase
{
    [HttpGet]
    public async Task<List<string>> GetNations()
    {
        return await allNationsByAccountQuery.ExecuteAsync();
    }
}
