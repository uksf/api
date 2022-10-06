using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Models;

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
        var pagedResult = await _getCommandMembersPagedQuery.ExecuteAsync(new(page, pageSize, query, sortMode, sortDirection, viewMode));

        return new(pagedResult.TotalCount, pagedResult.Data.Select(_commandMemberMapper.MapCommandMemberToAccount).ToList());
    }
}
