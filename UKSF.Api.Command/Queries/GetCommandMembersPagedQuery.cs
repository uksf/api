using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Queries
{
    public interface IGetCommandMembersPagedQuery
    {
        Task<PagedResult<DomainCommandMember>> ExecuteAsync(GetCommandMembersPagedQueryArgs args);
    }

    public class GetCommandMembersPagedQueryArgs
    {
        public GetCommandMembersPagedQueryArgs(
            int page,
            int pageSize,
            string query,
            CommandMemberSortMode sortMode,
            int sortDirection,
            CommandMemberViewMode viewMode
        )
        {
            Page = page;
            PageSize = pageSize;
            Query = query;
            SortMode = sortMode;
            SortDirection = sortDirection;
            ViewMode = viewMode;
        }

        public int Page { get; }
        public int PageSize { get; }
        public string Query { get; }
        public CommandMemberSortMode SortMode { get; }
        public int SortDirection { get; }
        public CommandMemberViewMode ViewMode { get; }
    }

    public class GetCommandMembersPagedQuery : IGetCommandMembersPagedQuery
    {
        private readonly IAccountContext _accountContext;
        private readonly IHttpContextService _httpContextService;

        public GetCommandMembersPagedQuery(IAccountContext accountContext, IHttpContextService httpContextService)
        {
            _accountContext = accountContext;
            _httpContextService = httpContextService;
        }

        public async Task<PagedResult<DomainCommandMember>> ExecuteAsync(GetCommandMembersPagedQueryArgs args)
        {
            var sortDefinition = BuildSortDefinition(args.SortMode, args.SortDirection);
            var viewModeFilterDefinition = BuildViewModeFilterDefinition(args.ViewMode);
            var queryFilterDefinition = _accountContext.BuildPagedComplexQuery(args.Query, BuildFiltersFromQueryPart);
            var filterDefinition = Builders<DomainCommandMember>.Filter.And(viewModeFilterDefinition, queryFilterDefinition);

            var pagedResult = _accountContext.GetPaged(args.Page, args.PageSize, BuildAggregator, sortDefinition, filterDefinition);
            return await Task.FromResult(pagedResult);
        }

        private static SortDefinition<DomainCommandMember> BuildSortDefinition(CommandMemberSortMode sortMode, int sortDirection)
        {
            var sortDocument = sortMode switch
            {
                CommandMemberSortMode.RANK => new() { { "rank.order", sortDirection }, { "lastname", sortDirection }, { "firstname", sortDirection } },
                CommandMemberSortMode.ROLE => new() { { "role.name", sortDirection }, { "lastname", sortDirection }, { "firstname", sortDirection } },
                _                          => new BsonDocument { { "lastname", sortDirection }, { "firstname", sortDirection } }
            };
            return new BsonDocumentSortDefinition<DomainCommandMember>(sortDocument);
        }

        private FilterDefinition<DomainCommandMember> BuildViewModeFilterDefinition(CommandMemberViewMode viewMode)
        {
            if (viewMode == CommandMemberViewMode.ALL)
            {
                return Builders<DomainCommandMember>.Filter.Empty;
            }

            var currentAccount = _accountContext.GetSingle(_httpContextService.GetUserId());
            var unitFilter = Builders<DomainCommandMember>.Filter.Eq(x => x.Unit.Name, currentAccount.UnitAssignment);

            if (viewMode == CommandMemberViewMode.COC)
            {
                var unitsFilter = Builders<DomainCommandMember>.Filter.ElemMatch(x => x.ParentUnits, x => x.Name == currentAccount.UnitAssignment);
                return Builders<DomainCommandMember>.Filter.Or(unitFilter, unitsFilter);
            }

            return unitFilter;
        }

        private static FilterDefinition<DomainCommandMember> BuildFiltersFromQueryPart(string queryPart)
        {
            var regex = new BsonRegularExpression(new Regex(queryPart, RegexOptions.IgnoreCase));
            var filters = new List<FilterDefinition<DomainCommandMember>>
            {
                Builders<DomainCommandMember>.Filter.Regex(x => x.Id, regex),
                Builders<DomainCommandMember>.Filter.Regex(x => x.Lastname, regex),
                Builders<DomainCommandMember>.Filter.Regex(x => x.Firstname, regex),
                Builders<DomainCommandMember>.Filter.Regex(x => x.Rank.Name, regex),
                Builders<DomainCommandMember>.Filter.Regex(x => x.Rank.Abbreviation, regex),
                Builders<DomainCommandMember>.Filter.Regex(x => x.Role.Name, regex),
                Builders<DomainCommandMember>.Filter.ElemMatch(x => x.Units, x => Regex.IsMatch(x.Name, queryPart, RegexOptions.IgnoreCase)),
                Builders<DomainCommandMember>.Filter.ElemMatch(x => x.Units, x => Regex.IsMatch(x.Shortname, queryPart, RegexOptions.IgnoreCase)),
                Builders<DomainCommandMember>.Filter.ElemMatch(x => x.ParentUnits, x => Regex.IsMatch(x.Name, queryPart, RegexOptions.IgnoreCase)),
                Builders<DomainCommandMember>.Filter.ElemMatch(x => x.ParentUnits, x => Regex.IsMatch(x.Shortname, queryPart, RegexOptions.IgnoreCase))
            };
            return Builders<DomainCommandMember>.Filter.Or(filters);
        }

        private static IAggregateFluent<DomainCommandMember> BuildAggregator(IMongoCollection<DomainAccount> collection)
        {
            return collection.Aggregate()
                             .Match(x => x.MembershipState == MembershipState.MEMBER)
                             .Lookup("ranks", "rank", "name", "rank")
                             .Unwind("rank")
                             .Lookup("roles", "roleAssignment", "name", "role")
                             .Unwind("role")
                             .Lookup("units", "unitAssignment", "name", "unit")
                             .Unwind("unit")
                             .Lookup("units", "_id", "members", "units")
                             .AppendStage<BsonDocument>(
                                 new BsonDocument(
                                     "$graphLookup",
                                     new BsonDocument
                                     {
                                         { "from", "units" },
                                         { "startWith", "$units.parent" },
                                         { "connectFromField", "parent" },
                                         { "connectToField", "_id" },
                                         { "as", "parentUnits" },
                                         { "maxDepth", 50 },
                                         { "depthField", "depthField" }
                                     }
                                 )
                             )
                             .As<DomainCommandMember>();
        }
    }

    public enum CommandMemberSortMode
    {
        NAME,
        RANK,
        ROLE,
        UNIT
    }

    public enum CommandMemberViewMode
    {
        ALL,
        COC,
        UNIT
    }
}
