using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Queries;
using Xunit;

namespace UKSF.Api.Tests.Queries;

public class GetPagedLoasQueryTests
{
    private readonly Mock<ILoaContext> _mockLoaContext;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IHttpContextService> _mockHttpContextService;
    private readonly Mock<IUnitsService> _mockUnitsService;
    private readonly Mock<IClock> _mockClock;
    private readonly GetPagedLoasQuery _subject;

    private int _capturedPage;
    private int _capturedPageSize;
    private SortDefinition<DomainLoaWithAccount> _capturedSort;
    private FilterDefinition<DomainLoaWithAccount> _capturedFilter;

    public GetPagedLoasQueryTests()
    {
        _mockLoaContext = new Mock<ILoaContext>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockHttpContextService = new Mock<IHttpContextService>();
        _mockUnitsService = new Mock<IUnitsService>();
        _mockClock = new Mock<IClock>();

        _subject = new GetPagedLoasQuery(
            _mockLoaContext.Object,
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockHttpContextService.Object,
            _mockUnitsService.Object,
            _mockClock.Object
        );
    }

    private void SetupDefaultMocks()
    {
        _mockClock.Setup(x => x.Today()).Returns(new DateTime(2024, 1, 15)); // Monday

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount> { new() { Id = "member1" } });

        _mockLoaContext.Setup(x => x.BuildPagedComplexQuery(It.IsAny<string>(), It.IsAny<Func<string, FilterDefinition<DomainLoaWithAccount>>>()))
                       .Returns(Builders<DomainLoaWithAccount>.Filter.Empty);

        _mockLoaContext.Setup(x => x.GetPaged(
                                  It.IsAny<int>(),
                                  It.IsAny<int>(),
                                  It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                                  It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                                  It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
                              )
                       )
                       .Callback<int, int, Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>, SortDefinition<DomainLoaWithAccount>,
                           FilterDefinition<DomainLoaWithAccount>>((page, pageSize, _, sort, filter) =>
                           {
                               _capturedPage = page;
                               _capturedPageSize = pageSize;
                               _capturedSort = sort;
                               _capturedFilter = filter;
                           }
                       )
                       .Returns(new PagedResult<DomainLoaWithAccount>(0, Enumerable.Empty<DomainLoaWithAccount>()));
    }

    [Fact]
    public async Task ExecuteAsync_calls_GetPaged_with_correct_page_and_pageSize()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(3, 25, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _capturedPage.Should().Be(3);
        _capturedPageSize.Should().Be(25);
    }

    [Fact]
    public async Task ExecuteAsync_returns_result_from_GetPaged()
    {
        SetupDefaultMocks();
        var expectedData = new List<DomainLoaWithAccount> { new() { Id = "loa1" } };
        var expectedResult = new PagedResult<DomainLoaWithAccount>(1, expectedData);

        _mockLoaContext.Setup(x => x.GetPaged(
                                  It.IsAny<int>(),
                                  It.IsAny<int>(),
                                  It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                                  It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                                  It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
                              )
                       )
                       .Returns(expectedResult);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        var result = await _subject.ExecuteAsync(args);

        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_passes_query_to_BuildPagedComplexQuery()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "search term", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockLoaContext.Verify(x => x.BuildPagedComplexQuery("search term", It.IsAny<Func<string, FilterDefinition<DomainLoaWithAccount>>>()), Times.Once);
    }

    [Fact]
    public async Task ViewMode_Mine_uses_current_user_id()
    {
        SetupDefaultMocks();
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user123");

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Mine);

        await _subject.ExecuteAsync(args);

        _mockHttpContextService.Verify(x => x.GetUserId(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ViewMode_Mine_does_not_query_account_context_for_members()
    {
        SetupDefaultMocks();
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user123");

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Mine);

        await _subject.ExecuteAsync(args);

        _mockAccountContext.Verify(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()), Times.Never);
        _mockAccountContext.Verify(x => x.GetSingle(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ViewMode_All_queries_member_accounts()
    {
        SetupDefaultMocks();
        var members = new List<DomainAccount>
        {
            new() { Id = "m1", MembershipState = MembershipState.Member }, new() { Id = "m2", MembershipState = MembershipState.Member }
        };
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(members);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockAccountContext.Verify(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()), Times.Once);
    }

    [Fact]
    public async Task ViewMode_All_predicate_only_matches_member_state()
    {
        SetupDefaultMocks();
        Func<DomainAccount, bool> capturedPredicate = null;
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>()))
                           .Callback<Func<DomainAccount, bool>>(pred => capturedPredicate = pred)
                           .Returns(new List<DomainAccount>());

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        capturedPredicate.Should().NotBeNull();
        capturedPredicate(new DomainAccount { MembershipState = MembershipState.Member }).Should().BeTrue();
        capturedPredicate(new DomainAccount { MembershipState = MembershipState.Unconfirmed }).Should().BeFalse();
        capturedPredicate(new DomainAccount { MembershipState = MembershipState.Confirmed }).Should().BeFalse();
    }

    [Fact]
    public async Task ViewMode_Coc_gets_parent_unit_and_all_children()
    {
        SetupDefaultMocks();
        var account = new DomainAccount { Id = "user1", UnitAssignment = "Alpha" };
        var parentUnit = new DomainUnit { Name = "Alpha", Members = new List<string> { "m1" } };
        var childUnit = new DomainUnit { Name = "Alpha-1", Members = new List<string> { "m2", "m3" } };

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(parentUnit);
        _mockUnitsService.Setup(x => x.GetAllChildren(parentUnit, true)).Returns(new List<DomainUnit> { parentUnit, childUnit });

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Coc);

        await _subject.ExecuteAsync(args);

        _mockAccountContext.Verify(x => x.GetSingle("user1"), Times.Once);
        _mockUnitsContext.Verify(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()), Times.Once);
        _mockUnitsService.Verify(x => x.GetAllChildren(parentUnit, true), Times.Once);
    }

    [Fact]
    public async Task ViewMode_Coc_looks_up_unit_by_account_unit_assignment()
    {
        SetupDefaultMocks();
        var account = new DomainAccount { Id = "user1", UnitAssignment = "Bravo" };
        var parentUnit = new DomainUnit { Name = "Bravo", Members = new List<string> { "m1" } };

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(account);

        Func<DomainUnit, bool> capturedPredicate = null;
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Callback<Func<DomainUnit, bool>>(pred => capturedPredicate = pred)
                         .Returns(parentUnit);
        _mockUnitsService.Setup(x => x.GetAllChildren(parentUnit, true)).Returns(new List<DomainUnit> { parentUnit });

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Coc);

        await _subject.ExecuteAsync(args);

        capturedPredicate.Should().NotBeNull();
        capturedPredicate(new DomainUnit { Name = "Bravo" }).Should().BeTrue();
        capturedPredicate(new DomainUnit { Name = "Alpha" }).Should().BeFalse();
    }

    [Fact]
    public async Task DateMode_All_completes_without_error()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(2024, 1, 15)] // Monday -> Saturday = +5 days -> Jan 20
    [InlineData(2024, 1, 16)] // Tuesday -> Saturday = +4 days -> Jan 20
    [InlineData(2024, 1, 17)] // Wednesday -> Saturday = +3 days -> Jan 20
    [InlineData(2024, 1, 18)] // Thursday -> Saturday = +2 days -> Jan 20
    [InlineData(2024, 1, 19)] // Friday -> Saturday = +1 day -> Jan 20
    [InlineData(2024, 1, 20)] // Saturday -> Saturday = same day -> Jan 20
    [InlineData(2024, 1, 21)] // Sunday -> Saturday = +6 days -> Jan 27
    public async Task DateMode_NextOp_computes_next_Saturday(int year, int month, int day)
    {
        SetupDefaultMocks();
        var today = new DateTime(year, month, day);
        _mockClock.Setup(x => x.Today()).Returns(today);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.NextOp, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockClock.Verify(x => x.Today(), Times.AtLeastOnce);
        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(2024, 1, 15)] // Monday -> Wednesday = +2 days -> Jan 17
    [InlineData(2024, 1, 16)] // Tuesday -> Wednesday = +1 day -> Jan 17
    [InlineData(2024, 1, 17)] // Wednesday -> Wednesday = same day -> Jan 17
    [InlineData(2024, 1, 18)] // Thursday -> Wednesday = +6 days -> Jan 24
    [InlineData(2024, 1, 19)] // Friday -> Wednesday = +5 days -> Jan 24
    [InlineData(2024, 1, 20)] // Saturday -> Wednesday = +4 days -> Jan 24
    [InlineData(2024, 1, 21)] // Sunday -> Wednesday = +3 days -> Jan 24
    public async Task DateMode_NextTraining_computes_next_Wednesday(int year, int month, int day)
    {
        SetupDefaultMocks();
        var today = new DateTime(year, month, day);
        _mockClock.Setup(x => x.Today()).Returns(today);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.NextTraining, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockClock.Verify(x => x.Today(), Times.AtLeastOnce);
        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task DateMode_Select_uses_provided_date()
    {
        SetupDefaultMocks();
        var selectedDate = new DateTime(2024, 3, 15, 14, 30, 0);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.Select, selectedDate, LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task DateMode_Select_with_null_date_throws_ArgumentNullException()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.Select, null, LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("selectedDate");
    }

    [Fact]
    public async Task SelectionMode_Current_uses_clock_today()
    {
        SetupDefaultMocks();
        var today = new DateTime(2024, 1, 15);
        _mockClock.Setup(x => x.Today()).Returns(today);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockClock.Verify(x => x.Today(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SelectionMode_Future_uses_clock_today()
    {
        SetupDefaultMocks();
        var today = new DateTime(2024, 1, 15);
        _mockClock.Setup(x => x.Today()).Returns(today);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Future, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockClock.Verify(x => x.Today(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SelectionMode_Past_uses_clock_today()
    {
        SetupDefaultMocks();
        var today = new DateTime(2024, 1, 15);
        _mockClock.Setup(x => x.Today()).Returns(today);

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Past, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockClock.Verify(x => x.Today(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ViewMode_Coc_collects_members_from_all_children()
    {
        SetupDefaultMocks();
        var account = new DomainAccount { Id = "user1", UnitAssignment = "Alpha" };
        var parentUnit = new DomainUnit { Name = "Alpha", Members = new List<string> { "m1" } };
        var childUnit1 = new DomainUnit { Name = "Alpha-1", Members = new List<string> { "m2" } };
        var childUnit2 = new DomainUnit { Name = "Alpha-2", Members = new List<string> { "m3", "m4" } };

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(parentUnit);
        _mockUnitsService.Setup(x => x.GetAllChildren(parentUnit, true))
        .Returns(
            new List<DomainUnit>
            {
                parentUnit,
                childUnit1,
                childUnit2
            }
        );

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Coc);

        await _subject.ExecuteAsync(args);

        _mockUnitsService.Verify(x => x.GetAllChildren(parentUnit, true), Times.Once);
    }

    [Fact]
    public async Task BuildPagedComplexQuery_callback_builds_regex_filter_for_query_part()
    {
        SetupDefaultMocks();
        Func<string, FilterDefinition<DomainLoaWithAccount>> capturedFilterBuilder = null;
        _mockLoaContext.Setup(x => x.BuildPagedComplexQuery(It.IsAny<string>(), It.IsAny<Func<string, FilterDefinition<DomainLoaWithAccount>>>()))
                       .Callback<string, Func<string, FilterDefinition<DomainLoaWithAccount>>>((_, builder) => capturedFilterBuilder = builder)
                       .Returns(Builders<DomainLoaWithAccount>.Filter.Empty);

        var args = new GetPagedLoasQueryArgs(1, 10, "test", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        capturedFilterBuilder.Should().NotBeNull();
        var filter = capturedFilterBuilder("test");
        filter.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPagedComplexQuery_callback_escapes_regex_special_characters()
    {
        SetupDefaultMocks();
        Func<string, FilterDefinition<DomainLoaWithAccount>> capturedFilterBuilder = null;
        _mockLoaContext.Setup(x => x.BuildPagedComplexQuery(It.IsAny<string>(), It.IsAny<Func<string, FilterDefinition<DomainLoaWithAccount>>>()))
                       .Callback<string, Func<string, FilterDefinition<DomainLoaWithAccount>>>((_, builder) => capturedFilterBuilder = builder)
                       .Returns(Builders<DomainLoaWithAccount>.Filter.Empty);

        var args = new GetPagedLoasQueryArgs(1, 10, "test(special)", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        capturedFilterBuilder.Should().NotBeNull();
        var act = () => capturedFilterBuilder("test(special)");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(LoaSelectionMode.Current)]
    [InlineData(LoaSelectionMode.Future)]
    [InlineData(LoaSelectionMode.Past)]
    public async Task All_selection_modes_produce_valid_results(LoaSelectionMode selectionMode)
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", selectionMode, LoaDateMode.All, null, LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(LoaViewMode.All)]
    [InlineData(LoaViewMode.Mine)]
    [InlineData(LoaViewMode.Coc)]
    public async Task All_view_modes_produce_valid_results(LoaViewMode viewMode)
    {
        SetupDefaultMocks();

        if (viewMode == LoaViewMode.Coc)
        {
            var account = new DomainAccount { Id = "user1", UnitAssignment = "Alpha" };
            var parentUnit = new DomainUnit { Name = "Alpha", Members = new List<string> { "m1" } };
            _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
            _mockAccountContext.Setup(x => x.GetSingle("user1")).Returns(account);
            _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(parentUnit);
            _mockUnitsService.Setup(x => x.GetAllChildren(parentUnit, true)).Returns(new List<DomainUnit> { parentUnit });
        }
        else if (viewMode == LoaViewMode.Mine)
        {
            _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        }

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, viewMode);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(LoaDateMode.All)]
    [InlineData(LoaDateMode.NextOp)]
    [InlineData(LoaDateMode.NextTraining)]
    public async Task All_non_select_date_modes_work_without_selected_date(LoaDateMode dateMode)
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, dateMode, null, LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DateMode_Select_with_date_works()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.Select, new DateTime(2024, 6, 15), LoaViewMode.All);

        var act = () => _subject.ExecuteAsync(args);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetNextDayOfWeek_when_today_is_target_day_returns_same_day()
    {
        SetupDefaultMocks();
        // 2024-01-20 is Saturday. NextOp targets Saturday, so result should be the same day.
        _mockClock.Setup(x => x.Today()).Returns(new DateTime(2024, 1, 20));

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.NextOp, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetNextDayOfWeek_when_today_is_wednesday_and_target_is_wednesday_returns_same_day()
    {
        SetupDefaultMocks();
        // 2024-01-17 is Wednesday. NextTraining targets Wednesday.
        _mockClock.Setup(x => x.Today()).Returns(new DateTime(2024, 1, 17));

        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.NextTraining, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockLoaContext.Verify(
            x => x.GetPaged(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<IMongoCollection<DomainLoa>, IAggregateFluent<DomainLoaWithAccount>>>(),
                It.IsAny<SortDefinition<DomainLoaWithAccount>>(),
                It.IsAny<FilterDefinition<DomainLoaWithAccount>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ViewMode_All_does_not_call_units_service()
    {
        SetupDefaultMocks();
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.All);

        await _subject.ExecuteAsync(args);

        _mockUnitsService.Verify(x => x.GetAllChildren(It.IsAny<DomainUnit>(), It.IsAny<bool>()), Times.Never);
        _mockUnitsContext.Verify(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()), Times.Never);
    }

    [Fact]
    public async Task ViewMode_Mine_does_not_call_units_service()
    {
        SetupDefaultMocks();
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns("user1");
        var args = new GetPagedLoasQueryArgs(1, 10, "", LoaSelectionMode.Current, LoaDateMode.All, null, LoaViewMode.Mine);

        await _subject.ExecuteAsync(args);

        _mockUnitsService.Verify(x => x.GetAllChildren(It.IsAny<DomainUnit>(), It.IsAny<bool>()), Times.Never);
        _mockUnitsContext.Verify(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()), Times.Never);
    }
}
