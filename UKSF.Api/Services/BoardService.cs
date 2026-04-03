using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IBoardService
{
    IEnumerable<DomainBoard> GetAccessibleBoards();
    DomainBoard GetBoard(string boardId);
    bool HasAccess(DomainBoard board);
    List<BasicAccount> ResolveAccessMembers(BoardPermissions permissions);
    Task<DomainBoard> CreateBoard(string name, BoardPermissions permissions);
    Task UpdateBoard(string boardId, string name, BoardPermissions permissions, List<string> labels);
    Task SoftDeleteBoard(string boardId);
    Task<BoardCard> CreateCard(string boardId, string title);
    Task UpdateCard(string boardId, string cardId, string title, string detail, List<string> labels, string assigneeId);
    Task DeleteCard(string boardId, string cardId);

    Task<(BoardCard Card, BoardColumnKey FromColumn, List<BoardCard> TargetColumnCards)> MoveCard(
        string boardId,
        string cardId,
        BoardColumnKey targetColumn,
        int targetIndex
    );

    List<BoardCard> GetDoneCards(string boardId, int skip, int take);
    Task<string> EnsureCommentThread(string boardId, string cardId);
}

public class BoardService(
    IBoardContext boardContext,
    IHttpContextService httpContextService,
    IUnitsService unitsService,
    IUnitsContext unitsContext,
    IDisplayNameService displayNameService,
    ICommentThreadContext commentThreadContext,
    IAccountContext accountContext
) : IBoardService
{
    public IEnumerable<DomainBoard> GetAccessibleBoards()
    {
        var boards = boardContext.Get().Where(b => !b.IsDeleted);

        if (httpContextService.UserHasPermission(Permissions.Admin))
        {
            return boards;
        }

        return boards.Where(HasAccess);
    }

    public DomainBoard GetBoard(string boardId)
    {
        return boardContext.GetSingle(boardId);
    }

    public bool HasAccess(DomainBoard board)
    {
        if (httpContextService.UserHasPermission(Permissions.Admin))
        {
            return true;
        }

        var userId = httpContextService.GetUserId();

        if (board.Permissions.Members.Contains(userId))
        {
            return true;
        }

        return board.Permissions.Units.Any(unitId => board.Permissions.ExpandToSubUnits
                                               ? unitsService.AnyChildHasMember(unitId, userId)
                                               : unitsService.HasMember(unitId, userId)
        );
    }

    public List<BasicAccount> ResolveAccessMembers(BoardPermissions permissions)
    {
        HashSet<string> memberIds = [..permissions.Members];

        foreach (var unitId in permissions.Units)
        {
            var unit = unitsContext.GetSingle(unitId);
            if (unit is null)
            {
                continue;
            }

            var units = permissions.ExpandToSubUnits ? unitsService.GetAllChildren(unit, true) : [unit];
            memberIds.UnionWith(units.SelectMany(u => u.Members));
        }

        memberIds.UnionWith(accountContext.Get(a => a.Admin && a.MembershipState == MembershipState.Member).Select(a => a.Id));

        return memberIds.Select(id => new BasicAccount { Id = id, DisplayName = displayNameService.GetDisplayName(id) }).ToList();
    }

    public async Task<DomainBoard> CreateBoard(string name, BoardPermissions permissions)
    {
        var board = new DomainBoard
        {
            Name = name,
            CreatedBy = httpContextService.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            Permissions = permissions,
            Columns =
            [
                new BoardColumn { Key = BoardColumnKey.Todo, Name = "Todo" },
                new BoardColumn { Key = BoardColumnKey.Blocked, Name = "Blocked" },
                new BoardColumn { Key = BoardColumnKey.InProgress, Name = "In Progress" },
                new BoardColumn { Key = BoardColumnKey.Review, Name = "Review" },
                new BoardColumn { Key = BoardColumnKey.Done, Name = "Done" }
            ]
        };

        await boardContext.Add(board);
        return board;
    }

    public async Task UpdateBoard(string boardId, string name, BoardPermissions permissions, List<string> labels)
    {
        var board = boardContext.GetSingle(boardId);
        board.Name = name;
        board.Permissions = permissions;
        board.Labels = labels;
        await boardContext.Replace(board);
    }

    public async Task SoftDeleteBoard(string boardId)
    {
        var board = boardContext.GetSingle(boardId);
        board.IsDeleted = true;
        await boardContext.Replace(board);
    }

    public async Task<BoardCard> CreateCard(string boardId, string title)
    {
        var board = boardContext.GetSingle(boardId);
        var todoColumn = board.Columns.First(c => c.Key == BoardColumnKey.Todo);
        var userId = httpContextService.GetUserId();

        var card = new BoardCard
        {
            Title = title,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Order = todoColumn.Cards.Count,
            Activity =
            [
                new BoardCardActivity
                {
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    Description = "created this card"
                }
            ]
        };

        todoColumn.Cards.Add(card);
        await boardContext.Replace(board);
        return card;
    }

    public async Task UpdateCard(string boardId, string cardId, string title, string detail, List<string> labels, string assigneeId)
    {
        var board = boardContext.GetSingle(boardId);
        var (card, _) = FindCard(board, cardId);
        var userId = httpContextService.GetUserId();

        if (card.Title != title)
        {
            card.Activity.Add(
                new BoardCardActivity
                {
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    Description = $"changed title to \"{title}\""
                }
            );
        }

        if (card.AssigneeId != assigneeId)
        {
            card.Activity.Add(
                new BoardCardActivity
                {
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    Description = "changed assignee"
                }
            );
        }

        card.Title = title;
        card.Detail = detail;
        card.Labels = labels ?? [];
        card.AssigneeId = assigneeId;

        foreach (var label in card.Labels.Where(l => !board.Labels.Contains(l)))
        {
            board.Labels.Add(label);
        }

        await boardContext.Replace(board);
    }

    public async Task DeleteCard(string boardId, string cardId)
    {
        var board = boardContext.GetSingle(boardId);
        var (card, column) = FindCard(board, cardId);

        column.Cards.Remove(card);
        RecalculateOrder(column.Cards);

        await boardContext.Replace(board);
    }

    public async Task<(BoardCard Card, BoardColumnKey FromColumn, List<BoardCard> TargetColumnCards)> MoveCard(
        string boardId,
        string cardId,
        BoardColumnKey targetColumn,
        int targetIndex
    )
    {
        var board = boardContext.GetSingle(boardId);
        var (card, sourceColumn) = FindCard(board, cardId);
        var fromColumnKey = sourceColumn.Key;
        var userId = httpContextService.GetUserId();

        sourceColumn.Cards.Remove(card);
        RecalculateOrder(sourceColumn.Cards);

        var destColumn = board.Columns.First(c => c.Key == targetColumn);
        var insertIndex = Math.Min(targetIndex, destColumn.Cards.Count);
        destColumn.Cards.Insert(insertIndex, card);
        RecalculateOrder(destColumn.Cards);

        if (targetColumn == BoardColumnKey.InProgress && fromColumnKey is BoardColumnKey.Todo or BoardColumnKey.Blocked)
        {
            card.AssigneeId ??= userId;
        }

        card.ClosedAt = targetColumn == BoardColumnKey.Done ? DateTime.UtcNow : null;

        card.Activity.Add(
            new BoardCardActivity
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Description = $"moved to {targetColumn}"
            }
        );

        await boardContext.Replace(board);
        return (card, fromColumnKey, destColumn.Cards);
    }

    public List<BoardCard> GetDoneCards(string boardId, int skip, int take)
    {
        var board = boardContext.GetSingle(boardId);
        var doneColumn = board.Columns.First(c => c.Key == BoardColumnKey.Done);

        return doneColumn.Cards.OrderByDescending(c => c.ClosedAt).Skip(skip).Take(take).ToList();
    }

    public async Task<string> EnsureCommentThread(string boardId, string cardId)
    {
        var board = boardContext.GetSingle(boardId);
        var (card, _) = FindCard(board, cardId);

        if (!string.IsNullOrEmpty(card.CommentThreadId))
        {
            return card.CommentThreadId;
        }

        var thread = new DomainCommentThread
        {
            Authors = [],
            Comments = [],
            Mode = ThreadMode.All
        };

        await commentThreadContext.Add(thread);

        card.CommentThreadId = thread.Id;
        await boardContext.Replace(board);

        return thread.Id;
    }

    private static (BoardCard Card, BoardColumn Column) FindCard(DomainBoard board, string cardId)
    {
        foreach (var column in board.Columns)
        {
            var card = column.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                return (card, column);
            }
        }

        throw new InvalidOperationException($"Card {cardId} not found on board {board.Id}");
    }

    private static void RecalculateOrder(List<BoardCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].Order = i;
        }
    }
}
