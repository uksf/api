using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Queries;

public interface IGetCommandMembersPagedQuery
{
    Task<PagedResult<CommandMemberAccount>> ExecuteAsync(GetCommandMembersPagedQueryArgs args);
}

public record GetCommandMembersPagedQueryArgs(
    int Page,
    int PageSize,
    string Query,
    CommandMemberSortMode SortMode,
    int SortDirection,
    CommandMemberViewMode ViewMode
);

public class GetCommandMembersPagedQuery(IAccountContext accountContext, IHttpContextService httpContextService) : IGetCommandMembersPagedQuery
{
    private static IAggregateFluent<CommandMemberAccount> BuildAggregator(IMongoCollection<DomainAccount> collection)
    {
        return collection.Aggregate()
                         .Match(x => x.MembershipState == MembershipState.Member)
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
                         .Lookup("training", "trainings", "_id", "trainings")
                         .As<CommandMemberAccount>();
    }

    public async Task<PagedResult<CommandMemberAccount>> ExecuteAsync(GetCommandMembersPagedQueryArgs args)
    {
        var aggregator = BuildAggregator;
        var sortDefinition = BuildSortDefinition(args.SortMode, args.SortDirection);
        var viewModeFilterDefinition = BuildViewModeFilterDefinition(args.ViewMode);
        var queryFilterDefinition = accountContext.BuildPagedComplexQuery(args.Query, BuildFiltersFromQueryPart);
        var filterDefinition = Builders<CommandMemberAccount>.Filter.And(viewModeFilterDefinition, queryFilterDefinition);

        var pagedResult = accountContext.GetPaged(args.Page, args.PageSize, aggregator, sortDefinition, filterDefinition);
        return await Task.FromResult(pagedResult);
    }

    private static SortDefinition<CommandMemberAccount> BuildSortDefinition(CommandMemberSortMode sortMode, int sortDirection)
    {
        var sortDocument = sortMode switch
        {
            CommandMemberSortMode.Rank => new BsonDocument
            {
                { "rank.order", sortDirection },
                { "lastname", sortDirection },
                { "firstname", sortDirection }
            },
            CommandMemberSortMode.Role => new BsonDocument
            {
                { "role.name", sortDirection },
                { "lastname", sortDirection },
                { "firstname", sortDirection }
            },
            _                          => new BsonDocument { { "lastname", sortDirection }, { "firstname", sortDirection } }
        };
        return new BsonDocumentSortDefinition<CommandMemberAccount>(sortDocument);
    }

    private FilterDefinition<CommandMemberAccount> BuildViewModeFilterDefinition(CommandMemberViewMode viewMode)
    {
        if (viewMode == CommandMemberViewMode.All)
        {
            return Builders<CommandMemberAccount>.Filter.Empty;
        }

        var currentAccount = accountContext.GetSingle(httpContextService.GetUserId());
        var unitFilter = Builders<CommandMemberAccount>.Filter.Eq(x => x.Unit.Name, currentAccount.UnitAssignment);

        if (viewMode == CommandMemberViewMode.Coc)
        {
            var unitsFilter = Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.ParentUnits, x => x.Name == currentAccount.UnitAssignment);
            return Builders<CommandMemberAccount>.Filter.Or(unitFilter, unitsFilter);
        }

        return unitFilter;
    }

    private static FilterDefinition<CommandMemberAccount> BuildFiltersFromQueryPart(string queryPart)
    {
        var regex = new BsonRegularExpression(new Regex(queryPart, RegexOptions.IgnoreCase));
        var filters = new List<FilterDefinition<CommandMemberAccount>>
        {
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Id, regex),
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Lastname, regex),
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Firstname, regex),
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Rank.Name, regex),
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Rank.Abbreviation, regex),
            Builders<CommandMemberAccount>.Filter.Regex(x => x.Role.Name, regex),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.Units, x => Regex.IsMatch(x.Name, queryPart, RegexOptions.IgnoreCase)),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.Units, x => Regex.IsMatch(x.Shortname, queryPart, RegexOptions.IgnoreCase)),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.ParentUnits, x => Regex.IsMatch(x.Name, queryPart, RegexOptions.IgnoreCase)),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.ParentUnits, x => Regex.IsMatch(x.Shortname, queryPart, RegexOptions.IgnoreCase)),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.Trainings, x => Regex.IsMatch(x.Name, queryPart, RegexOptions.IgnoreCase)),
            Builders<CommandMemberAccount>.Filter.ElemMatch(x => x.Trainings, x => Regex.IsMatch(x.ShortName, queryPart, RegexOptions.IgnoreCase))
        };
        return Builders<CommandMemberAccount>.Filter.Or(filters);
    }
}

public enum CommandMemberSortMode
{
    Name,
    Rank,
    Role,
    Unit
}

public enum CommandMemberViewMode
{
    All,
    Coc,
    Unit
}
