using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Queries
{
    public interface IGetPagedLoasQuery
    {
        Task<PagedResult<DomainLoaWithAccount>> ExecuteAsync(GetPagedLoasQueryArgs args);
    }

    public class GetPagedLoasQueryArgs
    {
        public GetPagedLoasQueryArgs(int page, int pageSize, string query, LoaSelectionMode selectionMode, LoaViewMode viewMode)
        {
            Page = page;
            PageSize = pageSize;
            Query = query;
            SelectionMode = selectionMode;
            ViewMode = viewMode;
        }

        public int Page { get; }
        public int PageSize { get; }
        public string Query { get; }
        public LoaSelectionMode SelectionMode { get; }
        public LoaViewMode ViewMode { get; }
    }

    public class GetPagedLoasQuery : IGetPagedLoasQuery
    {
        private readonly IAccountContext _accountContext;
        private readonly IHttpContextService _httpContextService;
        private readonly ILoaContext _loaContext;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public GetPagedLoasQuery(
            ILoaContext loaContext,
            IAccountContext accountContext,
            IUnitsContext unitsContext,
            IHttpContextService httpContextService,
            IUnitsService unitsService
        )
        {
            _loaContext = loaContext;
            _accountContext = accountContext;
            _unitsContext = unitsContext;
            _httpContextService = httpContextService;
            _unitsService = unitsService;
        }

        public async Task<PagedResult<DomainLoaWithAccount>> ExecuteAsync(GetPagedLoasQueryArgs args)
        {
            var sortDefinition = BuildSortDefinition(args.SelectionMode);
            var viewModeFilterDefinition = BuildViewModeFilterDefinition(args.ViewMode);
            var selectionModeFilterDefinition = BuildSelectionModeFilterDefinition(args.SelectionMode);
            var queryFilterDefinition = _loaContext.BuildPagedComplexQuery(args.Query, BuildFiltersFromQueryPart);
            var filterDefinition = Builders<DomainLoaWithAccount>.Filter.And(viewModeFilterDefinition, selectionModeFilterDefinition, queryFilterDefinition);

            var pagedResult = _loaContext.GetPaged(args.Page, args.PageSize, BuildAggregator, sortDefinition, filterDefinition);
            return await Task.FromResult(pagedResult);
        }

        private SortDefinition<DomainLoaWithAccount> BuildSortDefinition(LoaSelectionMode selectionMode)
        {
            BsonDocument sortDocument = selectionMode switch
            {
                LoaSelectionMode.CURRENT => new() { { "end", 1 }, { "start", 1 }, { "submitted", 1 } },
                LoaSelectionMode.FUTURE  => new() { { "start", 1 }, { "end", 1 }, { "submitted", 1 } },
                LoaSelectionMode.PAST    => new() { { "end", -1 }, { "start", -1 }, { "submitted", 1 } },
                _                        => throw new ArgumentOutOfRangeException(nameof(selectionMode))
            };
            return new BsonDocumentSortDefinition<DomainLoaWithAccount>(sortDocument);
        }

        private FilterDefinition<DomainLoaWithAccount> BuildViewModeFilterDefinition(LoaViewMode viewMode)
        {
            switch (viewMode)
            {
                case LoaViewMode.ALL:
                {
                    var memberIds = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER).Select(x => x.Id).ToList();
                    return Builders<DomainLoaWithAccount>.Filter.In(x => x.Recipient, memberIds);
                }
                case LoaViewMode.COC:
                {
                    var currentAccount = _accountContext.GetSingle(_httpContextService.GetUserId());
                    var parentUnit = _unitsContext.GetSingle(x => x.Name == currentAccount.UnitAssignment);
                    var cocUnits = _unitsService.GetAllChildren(parentUnit, true).ToList();
                    var memberIds = cocUnits.SelectMany(x => x.Members).ToList();
                    return Builders<DomainLoaWithAccount>.Filter.In(x => x.Recipient, memberIds);
                }
                case LoaViewMode.ME: return Builders<DomainLoaWithAccount>.Filter.Eq(x => x.Recipient, _httpContextService.GetUserId());
                default:             throw new ArgumentOutOfRangeException(nameof(viewMode));
            }
        }

        private FilterDefinition<DomainLoaWithAccount> BuildSelectionModeFilterDefinition(LoaSelectionMode selectionMode)
        {
            var now = DateTime.UtcNow;

            return selectionMode switch
            {
                LoaSelectionMode.CURRENT => Builders<DomainLoaWithAccount>.Filter.And(
                    Builders<DomainLoaWithAccount>.Filter.Lte(x => x.Start, now),
                    Builders<DomainLoaWithAccount>.Filter.Gt(x => x.End, now)
                ),
                LoaSelectionMode.FUTURE => Builders<DomainLoaWithAccount>.Filter.Gte(x => x.Start, now),
                LoaSelectionMode.PAST   => Builders<DomainLoaWithAccount>.Filter.Lt(x => x.End, now),
                _                       => throw new ArgumentOutOfRangeException(nameof(selectionMode))
            };
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
                Builders<DomainLoaWithAccount>.Filter.Regex(x => x.Unit.Shortname, regex)
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
    }

    public enum LoaSelectionMode
    {
        CURRENT,
        FUTURE,
        PAST
    }

    public enum LoaViewMode
    {
        ALL,
        COC,
        ME
    }
}
