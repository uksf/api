using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Queries;

public enum LoaSelectionMode
{
    Current,
    Future,
    Past,
}

public enum LoaViewMode
{
    All,
    Coc,
    Mine,
}

public enum LoaDateMode
{
    All,
    NextOp,
    NextTraining,
    Select,
}

public interface IGetPagedLoasQuery
{
    Task<PagedResult<DomainLoaWithAccount>> ExecuteAsync(GetPagedLoasQueryArgs args);
}

public record GetPagedLoasQueryArgs(
    int Page,
    int PageSize,
    string Query,
    LoaSelectionMode SelectionMode,
    LoaDateMode DateMode,
    DateTime? SelectedDate,
    LoaViewMode ViewMode
);

public class GetPagedLoasQuery : IGetPagedLoasQuery
{
    private readonly IAccountContext _accountContext;
    private readonly IHttpContextService _httpContextService;
    private readonly ILoaContext _loaContext;
    private readonly IUnitsContext _unitsContext;
    private readonly IUnitsService _unitsService;
    private readonly IClock _clock;

    public GetPagedLoasQuery(
        ILoaContext loaContext,
        IAccountContext accountContext,
        IUnitsContext unitsContext,
        IHttpContextService httpContextService,
        IUnitsService unitsService,
        IClock clock
    )
    {
        _loaContext = loaContext;
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _httpContextService = httpContextService;
        _unitsService = unitsService;
        _clock = clock;
    }

    public async Task<PagedResult<DomainLoaWithAccount>> ExecuteAsync(GetPagedLoasQueryArgs args)
    {
        var sorting = BuildSorting(args.SelectionMode);
        var selectionModeFilter = BuildSelectionModeFilter(args.SelectionMode);
        var dateModeFilter = BuildDateModeFilter(args.DateMode, args.SelectedDate);
        var viewModeFilter = BuildViewModeFilter(args.ViewMode);
        var queryFilter = _loaContext.BuildPagedComplexQuery(args.Query, BuildFiltersFromQueryPart);
        var filter = Builders<DomainLoaWithAccount>.Filter.And(selectionModeFilter, dateModeFilter, viewModeFilter, queryFilter);

        var pagedResult = _loaContext.GetPaged(args.Page, args.PageSize, BuildAggregator, sorting, filter);
        return await Task.FromResult(pagedResult);
    }

    private static SortDefinition<DomainLoaWithAccount> BuildSorting(LoaSelectionMode selectionMode)
    {
        var startSorter = Builders<DomainLoaWithAccount>.Sort.Ascending(x => x.Start);
        var endSorter = Builders<DomainLoaWithAccount>.Sort.Ascending(x => x.End);
        var submittedSorter = Builders<DomainLoaWithAccount>.Sort.Ascending(x => x.Submitted);

        switch (selectionMode)
        {
            case LoaSelectionMode.Current: return Builders<DomainLoaWithAccount>.Sort.Combine(endSorter, startSorter, submittedSorter);
            case LoaSelectionMode.Future:  return Builders<DomainLoaWithAccount>.Sort.Combine(startSorter, endSorter, submittedSorter);
            case LoaSelectionMode.Past:
                startSorter = Builders<DomainLoaWithAccount>.Sort.Descending(x => x.Start);
                endSorter = Builders<DomainLoaWithAccount>.Sort.Descending(x => x.End);
                return Builders<DomainLoaWithAccount>.Sort.Combine(endSorter, startSorter, submittedSorter);
            default: throw new ArgumentOutOfRangeException(nameof(selectionMode));
        }
    }

    private FilterDefinition<DomainLoaWithAccount> BuildSelectionModeFilter(LoaSelectionMode selectionMode)
    {
        var today = _clock.Today();

        return selectionMode switch
        {
            LoaSelectionMode.Current => Builders<DomainLoaWithAccount>.Filter.And(
                Builders<DomainLoaWithAccount>.Filter.Lte(x => x.Start, today),
                Builders<DomainLoaWithAccount>.Filter.Gte(x => x.End, today)
            ),
            LoaSelectionMode.Future => Builders<DomainLoaWithAccount>.Filter.Gt(x => x.Start, today),
            LoaSelectionMode.Past   => Builders<DomainLoaWithAccount>.Filter.Lt(x => x.End, today),
            _                       => throw new ArgumentOutOfRangeException(nameof(selectionMode)),
        };
    }

    private FilterDefinition<DomainLoaWithAccount> BuildDateModeFilter(LoaDateMode dateMode, DateTime? selectedDate)
    {
        if (dateMode == LoaDateMode.All)
        {
            return Builders<DomainLoaWithAccount>.Filter.Empty;
        }

        selectedDate = selectedDate?.Date;

        var filterDate = dateMode switch
        {
            LoaDateMode.NextOp       => GetNextDayOfWeek(DayOfWeek.Saturday),
            LoaDateMode.NextTraining => GetNextDayOfWeek(DayOfWeek.Wednesday),
            LoaDateMode.Select       => selectedDate ?? throw new ArgumentNullException(nameof(selectedDate), "Date must be selected"),
            LoaDateMode.All          => throw new ArgumentOutOfRangeException(nameof(dateMode)),
            _                        => throw new ArgumentOutOfRangeException(nameof(dateMode)),
        };

        filterDate = DateTime.SpecifyKind(filterDate, DateTimeKind.Utc);

        var startBeforeOrOnDateFilter = Builders<DomainLoaWithAccount>.Filter.Lte(x => x.Start, filterDate);
        var endOnDateOrAfterFilter = Builders<DomainLoaWithAccount>.Filter.Gte(x => x.End, filterDate);
        return Builders<DomainLoaWithAccount>.Filter.And(startBeforeOrOnDateFilter, endOnDateOrAfterFilter);
    }

    private FilterDefinition<DomainLoaWithAccount> BuildViewModeFilter(LoaViewMode viewMode)
    {
        switch (viewMode)
        {
            case LoaViewMode.All:
            {
                var memberIds = _accountContext.Get(x => x.MembershipState == MembershipState.Member).Select(x => x.Id).ToList();
                return Builders<DomainLoaWithAccount>.Filter.In(x => x.Recipient, memberIds);
            }
            case LoaViewMode.Coc:
            {
                var currentAccount = _accountContext.GetSingle(_httpContextService.GetUserId());
                var parentUnit = _unitsContext.GetSingle(x => x.Name == currentAccount.UnitAssignment);
                var cocUnits = _unitsService.GetAllChildren(parentUnit, true).ToList();
                var memberIds = cocUnits.SelectMany(x => x.Members).ToList();
                return Builders<DomainLoaWithAccount>.Filter.In(x => x.Recipient, memberIds);
            }
            case LoaViewMode.Mine: return Builders<DomainLoaWithAccount>.Filter.Eq(x => x.Recipient, _httpContextService.GetUserId());
            default:               throw new ArgumentOutOfRangeException(nameof(viewMode));
        }
    }

    private static FilterDefinition<DomainLoaWithAccount> BuildFiltersFromQueryPart(string queryPart)
    {
        var regex = new BsonRegularExpression(new Regex(queryPart, RegexOptions.IgnoreCase));
        var filters = new List<FilterDefinition<DomainLoaWithAccount>>
        {
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Id, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Account.Lastname, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Account.Firstname, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Rank.Name, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Rank.Abbreviation, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Unit.Name, regex),
            Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Unit.Shortname, regex),
        };
        return Builders<DomainLoaWithAccount>.Filter.Or(filters);
    }

    private static IAggregateFluent<DomainLoaWithAccount> BuildAggregator(IMongoCollection<DomainLoa> collection)
    {
        return collection.Aggregate()
                         .Lookup("accounts", "recipient", "_id", "account")
                         .Unwind("account")
                         .Lookup("ranks", "account.rank", "name", "rank")
                         .Unwind("rank")
                         .Lookup("units", "account.unitAssignment", "name", "unit")
                         .Unwind("unit")
                         .As<DomainLoaWithAccount>();
    }

    private DateTime GetNextDayOfWeek(DayOfWeek dayOfWeek)
    {
        var today = _clock.Today();
        var daysToAdd = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(daysToAdd);
    }
}
