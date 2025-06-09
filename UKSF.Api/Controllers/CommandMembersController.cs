using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("command/members")]
[Permissions(Permissions.Command)]
public class CommandMembersController(IGetCommandMembersPagedQuery getCommandMembersPagedQuery, ICommandMemberMapper commandMemberMapper) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<Account>> GetPaged(
        [FromQuery] int page,
        [FromQuery] int pageSize = 15,
        [FromQuery] string query = null,
        [FromQuery] CommandMemberSortMode sortMode = default,
        [FromQuery] int sortDirection = -1,
        [FromQuery] CommandMemberViewMode viewMode = default
    )
    {
        var pagedResult =
            await getCommandMembersPagedQuery.ExecuteAsync(new GetCommandMembersPagedQueryArgs(page, pageSize, query, sortMode, sortDirection, viewMode));

        return new PagedResult<Account>(pagedResult.TotalCount, pagedResult.Data.Select(commandMemberMapper.MapCommandMemberToAccount).ToList());
    }
}
