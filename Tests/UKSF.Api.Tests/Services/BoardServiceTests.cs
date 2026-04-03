using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class BoardServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IBoardContext> _mockBoardContext = new();
    private readonly Mock<ICommentThreadContext> _mockCommentThreadContext = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IUnitsService> _mockUnitsService = new();
    private readonly BoardService _subject;
    private readonly string _userId = ObjectId.GenerateNewId().ToString();

    public BoardServiceTests()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_userId);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns([]);

        _subject = new BoardService(
            _mockBoardContext.Object,
            _mockHttpContextService.Object,
            _mockUnitsService.Object,
            _mockUnitsContext.Object,
            _mockDisplayNameService.Object,
            _mockCommentThreadContext.Object,
            _mockAccountContext.Object
        );
    }

    [Fact]
    public void GetAccessibleBoards_FiltersDeletedBoards()
    {
        var board1 = new DomainBoard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Active",
            IsDeleted = false
        };
        var board2 = new DomainBoard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Deleted",
            IsDeleted = true
        };
        var board3 = new DomainBoard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Also Active",
            IsDeleted = false
        };

        _mockBoardContext.Setup(x => x.Get()).Returns([board1, board2, board3]);
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(true);

        var result = _subject.GetAccessibleBoards().ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(b => b.Name == "Active");
        result.Should().Contain(b => b.Name == "Also Active");
        result.Should().NotContain(b => b.Name == "Deleted");
    }

    [Fact]
    public void GetAccessibleBoards_AdminSeesAllNonDeletedBoards()
    {
        var board1 = new DomainBoard { Id = ObjectId.GenerateNewId().ToString(), Name = "Board 1" };
        var board2 = new DomainBoard { Id = ObjectId.GenerateNewId().ToString(), Name = "Board 2" };

        _mockBoardContext.Setup(x => x.Get()).Returns([board1, board2]);
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(true);

        var result = _subject.GetAccessibleBoards().ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void HasAccess_ReturnsTrueForExplicitMember()
    {
        var board = new DomainBoard { Permissions = new BoardPermissions { Members = [_userId] } };

        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);

        var result = _subject.HasAccess(board);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAccess_ReturnsTrueForUnitMember()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var board = new DomainBoard { Permissions = new BoardPermissions { Units = [unitId], ExpandToSubUnits = true } };

        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);
        _mockUnitsService.Setup(x => x.AnyChildHasMember(unitId, _userId)).Returns(true);

        var result = _subject.HasAccess(board);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasAccess_ReturnsFalseForNonMember()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var board = new DomainBoard
        {
            Permissions = new BoardPermissions
            {
                Units = [unitId],
                Members = [ObjectId.GenerateNewId().ToString()],
                ExpandToSubUnits = true
            }
        };

        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);
        _mockUnitsService.Setup(x => x.AnyChildHasMember(unitId, _userId)).Returns(false);

        var result = _subject.HasAccess(board);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasAccess_WithExpandToSubUnitsFalse_ChecksExactUnit()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var board = new DomainBoard { Permissions = new BoardPermissions { Units = [unitId], ExpandToSubUnits = false } };

        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);
        _mockUnitsService.Setup(x => x.HasMember(unitId, _userId)).Returns(true);

        var result = _subject.HasAccess(board);

        result.Should().BeTrue();
        _mockUnitsService.Verify(x => x.AnyChildHasMember(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateBoard_SetsCreatedByAndDefaultColumns()
    {
        DomainBoard capturedBoard = null;
        _mockBoardContext.Setup(x => x.Add(It.IsAny<DomainBoard>())).Callback<DomainBoard>(b => capturedBoard = b).Returns(Task.CompletedTask);

        var permissions = new BoardPermissions { Members = [_userId] };
        var result = await _subject.CreateBoard("Test Board", permissions);

        capturedBoard.Should().NotBeNull();
        capturedBoard.Name.Should().Be("Test Board");
        capturedBoard.CreatedBy.Should().Be(_userId);
        capturedBoard.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        capturedBoard.Permissions.Should().Be(permissions);
        capturedBoard.Columns.Should().HaveCount(5);
        capturedBoard.Columns[0].Key.Should().Be(BoardColumnKey.Todo);
        capturedBoard.Columns[1].Key.Should().Be(BoardColumnKey.Blocked);
        capturedBoard.Columns[2].Key.Should().Be(BoardColumnKey.InProgress);
        capturedBoard.Columns[3].Key.Should().Be(BoardColumnKey.Review);
        capturedBoard.Columns[4].Key.Should().Be(BoardColumnKey.Done);
    }

    [Fact]
    public void ResolveAccessMembers_CombinesUnitMembersAndExplicitMembers()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var unitMemberId = ObjectId.GenerateNewId().ToString();
        var explicitMemberId = ObjectId.GenerateNewId().ToString();

        var unit = new DomainUnit { Id = unitId, Members = [unitMemberId] };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockUnitsService.Setup(x => x.GetAllChildren(unit, true)).Returns([unit]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(unitMemberId)).Returns("Unit Member");
        _mockDisplayNameService.Setup(x => x.GetDisplayName(explicitMemberId)).Returns("Explicit Member");

        var permissions = new BoardPermissions
        {
            Units = [unitId],
            Members = [explicitMemberId],
            ExpandToSubUnits = true
        };

        var result = _subject.ResolveAccessMembers(permissions);

        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Id == unitMemberId && a.DisplayName == "Unit Member");
        result.Should().Contain(a => a.Id == explicitMemberId && a.DisplayName == "Explicit Member");
    }

    [Fact]
    public void ResolveAccessMembers_DeduplicatesMembers()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var sharedMemberId = ObjectId.GenerateNewId().ToString();

        var unit = new DomainUnit { Id = unitId, Members = [sharedMemberId] };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockUnitsService.Setup(x => x.GetAllChildren(unit, true)).Returns([unit]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(sharedMemberId)).Returns("Shared Member");

        var permissions = new BoardPermissions
        {
            Units = [unitId],
            Members = [sharedMemberId],
            ExpandToSubUnits = true
        };

        var result = _subject.ResolveAccessMembers(permissions);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(sharedMemberId);
    }

    [Fact]
    public void ResolveAccessMembers_IncludesAdminAccounts()
    {
        var adminId = ObjectId.GenerateNewId().ToString();
        var adminAccount = new DomainAccount
        {
            Id = adminId,
            Admin = true,
            MembershipState = MembershipState.Member
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns([adminAccount]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(adminId)).Returns("Admin User");

        var permissions = new BoardPermissions
        {
            Units = [],
            Members = [],
            ExpandToSubUnits = false
        };

        var result = _subject.ResolveAccessMembers(permissions);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(adminId);
        result.Single().DisplayName.Should().Be("Admin User");
    }

    [Fact]
    public void ResolveAccessMembers_DeduplicatesAdminWhoIsAlsoExplicitMember()
    {
        var adminId = ObjectId.GenerateNewId().ToString();
        var adminAccount = new DomainAccount
        {
            Id = adminId,
            Admin = true,
            MembershipState = MembershipState.Member
        };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns([adminAccount]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(adminId)).Returns("Admin User");

        var permissions = new BoardPermissions
        {
            Units = [],
            Members = [adminId],
            ExpandToSubUnits = false
        };

        var result = _subject.ResolveAccessMembers(permissions);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(adminId);
    }

    [Fact]
    public void ResolveAccessMembers_WithExpandToSubUnitsFalse_OnlyUsesDirectUnitMembers()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var memberId = ObjectId.GenerateNewId().ToString();

        var unit = new DomainUnit { Id = unitId, Members = [memberId] };
        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(memberId)).Returns("Direct Member");

        var permissions = new BoardPermissions
        {
            Units = [unitId],
            Members = [],
            ExpandToSubUnits = false
        };

        var result = _subject.ResolveAccessMembers(permissions);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(memberId);
        _mockUnitsService.Verify(x => x.GetAllChildren(It.IsAny<DomainUnit>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CreateCard_AddsToTodoColumnWithActivity()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var board = new DomainBoard
        {
            Id = boardId,
            Columns =
            [
                new BoardColumn { Key = BoardColumnKey.Todo, Cards = [] },
                new BoardColumn { Key = BoardColumnKey.Done, Cards = [] }
            ]
        };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        var result = await _subject.CreateCard(boardId, "New Card");

        result.Title.Should().Be("New Card");
        result.CreatedBy.Should().Be(_userId);
        result.Activity.Should().HaveCount(1);
        result.Activity[0].Description.Should().Contain("created");
        board.Columns[0].Cards.Should().Contain(result);
    }

    [Fact]
    public async Task MoveCard_SetsAssigneeOnMoveToInProgress()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card",
            Order = 0
        };
        var board = new DomainBoard
        {
            Id = boardId,
            Columns =
            [
                new BoardColumn { Key = BoardColumnKey.Todo, Cards = [card] },
                new BoardColumn { Key = BoardColumnKey.InProgress, Cards = [] },
                new BoardColumn { Key = BoardColumnKey.Done, Cards = [] }
            ]
        };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        var (movedCard, fromColumn, _) = await _subject.MoveCard(boardId, card.Id, BoardColumnKey.InProgress, 0);

        movedCard.AssigneeId.Should().Be(_userId);
        fromColumn.Should().Be(BoardColumnKey.Todo);
    }

    [Fact]
    public async Task MoveCard_SetsClosedAtOnMoveToDone()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card",
            Order = 0
        };
        var board = new DomainBoard
        {
            Id = boardId,
            Columns =
            [
                new BoardColumn { Key = BoardColumnKey.InProgress, Cards = [card] },
                new BoardColumn { Key = BoardColumnKey.Done, Cards = [] }
            ]
        };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        var (movedCard, _, _) = await _subject.MoveCard(boardId, card.Id, BoardColumnKey.Done, 0);

        movedCard.ClosedAt.Should().NotBeNull();
        movedCard.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteCard_RemovesCardAndRecalculatesOrder()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card1 = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card 1",
            Order = 0
        };
        var card2 = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card 2",
            Order = 1
        };
        var card3 = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card 3",
            Order = 2
        };
        var board = new DomainBoard { Id = boardId, Columns = [new BoardColumn { Key = BoardColumnKey.Todo, Cards = [card1, card2, card3] }] };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        await _subject.DeleteCard(boardId, card2.Id);

        var todoCards = board.Columns[0].Cards;
        todoCards.Should().HaveCount(2);
        todoCards[0].Order.Should().Be(0);
        todoCards[1].Order.Should().Be(1);
        todoCards.Should().NotContain(c => c.Id == card2.Id);
    }

    [Fact]
    public void GetDoneCards_ReturnsPaginatedCardsSortedByClosedAt()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card1 = new BoardCard
        {
            Id = "c1",
            Title = "Old",
            ClosedAt = DateTime.UtcNow.AddDays(-3)
        };
        var card2 = new BoardCard
        {
            Id = "c2",
            Title = "Recent",
            ClosedAt = DateTime.UtcNow.AddDays(-1)
        };
        var card3 = new BoardCard
        {
            Id = "c3",
            Title = "Middle",
            ClosedAt = DateTime.UtcNow.AddDays(-2)
        };
        var board = new DomainBoard { Id = boardId, Columns = [new BoardColumn { Key = BoardColumnKey.Done, Cards = [card1, card2, card3] }] };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);

        var result = _subject.GetDoneCards(boardId, 0, 2);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("c2");
        result[1].Id.Should().Be("c3");
    }

    [Fact]
    public async Task EnsureCommentThread_CreatesThreadIfCardHasNone()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card",
            CommentThreadId = null
        };
        var board = new DomainBoard { Id = boardId, Columns = [new BoardColumn { Key = BoardColumnKey.Todo, Cards = [card] }] };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockCommentThreadContext.Setup(x => x.Add(It.IsAny<DomainCommentThread>())).Returns(Task.CompletedTask);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        var threadId = await _subject.EnsureCommentThread(boardId, card.Id);

        threadId.Should().NotBeNullOrEmpty();
        card.CommentThreadId.Should().Be(threadId);
        _mockCommentThreadContext.Verify(x => x.Add(It.IsAny<DomainCommentThread>()), Times.Once);
    }

    [Fact]
    public async Task EnsureCommentThread_ReturnsExistingThreadId()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var existingThreadId = ObjectId.GenerateNewId().ToString();
        var card = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card",
            CommentThreadId = existingThreadId
        };
        var board = new DomainBoard { Id = boardId, Columns = [new BoardColumn { Key = BoardColumnKey.Todo, Cards = [card] }] };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);

        var threadId = await _subject.EnsureCommentThread(boardId, card.Id);

        threadId.Should().Be(existingThreadId);
        _mockCommentThreadContext.Verify(x => x.Add(It.IsAny<DomainCommentThread>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCard_AddsNewLabelsToBoardPool()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var card = new BoardCard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = "Card",
            Labels = []
        };
        var board = new DomainBoard
        {
            Id = boardId,
            Labels = ["existing-label"],
            Columns = [new BoardColumn { Key = BoardColumnKey.Todo, Cards = [card] }]
        };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        await _subject.UpdateCard(boardId, card.Id, "Updated", "Detail", ["existing-label", "new-label"], null);

        board.Labels.Should().Contain("new-label");
        board.Labels.Should().Contain("existing-label");
        card.Title.Should().Be("Updated");
        card.Detail.Should().Be("Detail");
    }

    [Fact]
    public async Task SoftDeleteBoard_SetsIsDeletedTrue()
    {
        var boardId = ObjectId.GenerateNewId().ToString();
        var board = new DomainBoard { Id = boardId, IsDeleted = false };

        _mockBoardContext.Setup(x => x.GetSingle(boardId)).Returns(board);
        _mockBoardContext.Setup(x => x.Replace(It.IsAny<DomainBoard>())).Returns(Task.CompletedTask);

        await _subject.SoftDeleteBoard(boardId);

        board.IsDeleted.Should().BeTrue();
        _mockBoardContext.Verify(x => x.Replace(board), Times.Once);
    }

    [Fact]
    public void GetAccessibleBoards_NonAdminOnlySeesAccessibleBoards()
    {
        var unitId = ObjectId.GenerateNewId().ToString();
        var accessibleBoard = new DomainBoard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Accessible",
            Permissions = new BoardPermissions { Members = [_userId] }
        };
        var inaccessibleBoard = new DomainBoard
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Inaccessible",
            Permissions = new BoardPermissions { Members = [ObjectId.GenerateNewId().ToString()] }
        };

        _mockBoardContext.Setup(x => x.Get()).Returns([accessibleBoard, inaccessibleBoard]);
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Admin)).Returns(false);

        var result = _subject.GetAccessibleBoards().ToList();

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Accessible");
    }
}
