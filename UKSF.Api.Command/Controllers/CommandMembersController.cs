using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Mappers;
using UKSF.Api.Command.Queries;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared;

namespace UKSF.Api.Command.Controllers
{
    [Route("command/members"), Permissions(Permissions.COMMAND)]
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
}
