using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Queries;

public enum ApplicationSortMode
{
    Name,
    DateApplied,
    DateCompleted
}

public interface IGetCompletedApplicationsPagedQueryHandler
{
    Task<PagedResult<DomainAccount>> ExecuteAsync(GetCompletedApplicationsPagedQuery query);
}

public record GetCompletedApplicationsPagedQuery(
    int Page,
    int PageSize,
    string Query,
    ApplicationSortMode ApplicationSortMode,
    int SortDirection,
    string RecruiterId
);

public class GetCompletedApplicationsPagedQueryHandler(IAccountContext accountContext) : IGetCompletedApplicationsPagedQueryHandler
{
    public async Task<PagedResult<DomainAccount>> ExecuteAsync(GetCompletedApplicationsPagedQuery query)
    {
        var aggregator = BuildAggregator;
        var sortDefinition = BuildSortDefinition(query.ApplicationSortMode, query.SortDirection);
        var recruiterFilterDefinition = BuildRecruiterFilterDefinition(query.RecruiterId);
        var queryFilterDefinition = accountContext.BuildPagedComplexQuery(query.Query, BuildFiltersFromQueryPart);
        var filterDefinition = Builders<DomainAccount>.Filter.And(recruiterFilterDefinition, queryFilterDefinition);

        var pagedResult = accountContext.GetPaged(query.Page, query.PageSize, aggregator, sortDefinition, filterDefinition);
        return await Task.FromResult(pagedResult);
    }

    private static IAggregateFluent<DomainAccount> BuildAggregator(IMongoCollection<DomainAccount> collection)
    {
        return collection.Aggregate().Match(x => x.Application != null && x.Application.State != ApplicationState.Waiting).As<DomainAccount>();
    }

    private static SortDefinition<DomainAccount> BuildSortDefinition(ApplicationSortMode sortMode, int sortDirection)
    {
        return new BsonDocumentSortDefinition<DomainAccount>(
            sortMode switch
            {
                ApplicationSortMode.Name => new BsonDocument { { "lastname", sortDirection }, { "firstname", sortDirection } },
                ApplicationSortMode.DateApplied => new BsonDocument
                {
                    { "application.dateCreated", sortDirection },
                    { "lastname", sortDirection },
                    { "firstname", sortDirection }
                },
                ApplicationSortMode.DateCompleted => new BsonDocument
                {
                    { "application.dateAccepted", sortDirection },
                    { "lastname", sortDirection },
                    { "firstname", sortDirection }
                },
                _ => throw new ArgumentOutOfRangeException(nameof(sortMode), sortMode, null)
            }
        );
    }

    private FilterDefinition<DomainAccount> BuildRecruiterFilterDefinition(string recruiterId)
    {
        return string.IsNullOrWhiteSpace(recruiterId)
            ? Builders<DomainAccount>.Filter.Empty
            : Builders<DomainAccount>.Filter.Eq(x => x.Application.Recruiter, recruiterId);
    }

    private static FilterDefinition<DomainAccount> BuildFiltersFromQueryPart(string queryPart)
    {
        var regex = new BsonRegularExpression(new Regex(queryPart, RegexOptions.IgnoreCase));
        var filters = new List<FilterDefinition<DomainAccount>>
        {
            Builders<DomainAccount>.Filter.Regex(x => x.Id, regex),
            Builders<DomainAccount>.Filter.Regex(x => x.Lastname, regex),
            Builders<DomainAccount>.Filter.Regex(x => x.Firstname, regex)
        };

        return Builders<DomainAccount>.Filter.Or(filters);
    }
}
