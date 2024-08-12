using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("command/members")]
[Permissions(Permissions.Command)]
public class CommandMembersController : ControllerBase
{
    private readonly ICommandMemberMapper _commandMemberMapper;
    private readonly IGetCommandMembersPagedQuery _getCommandMembersPagedQuery;

    public CommandMembersController(IGetCommandMembersPagedQuery getCommandMembersPagedQuery, ICommandMemberMapper commandMemberMapper)
    {
        _getCommandMembersPagedQuery = getCommandMembersPagedQuery;
        _commandMemberMapper = commandMemberMapper;
    }

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
            await _getCommandMembersPagedQuery.ExecuteAsync(new GetCommandMembersPagedQueryArgs(page, pageSize, query, sortMode, sortDirection, viewMode));

        return new PagedResult<Account>(pagedResult.TotalCount, pagedResult.Data.Select(_commandMemberMapper.MapCommandMemberToAccount).ToList());
    }
}
