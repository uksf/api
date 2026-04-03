using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.Controllers;

[Route("boards")]
[Permissions(Permissions.Member)]
public class BoardsController(IBoardService boardService, IHubContext<BoardHub, IBoardClient> boardHub, IDisplayNameService displayNameService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<BoardListItem> GetBoards()
    {
        return boardService.GetAccessibleBoards()
        .Select(b => new BoardListItem
            {
                Id = b.Id,
                Name = b.Name,
                MemberCount = boardService.ResolveAccessMembers(b.Permissions).Count
            }
        );
    }

    [HttpGet("{boardId}")]
    [Authorize]
    public BoardResponse GetBoard([FromRoute] string boardId)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        var members = boardService.ResolveAccessMembers(board.Permissions);

        return new BoardResponse
        {
            Id = board.Id,
            Name = board.Name,
            Labels = board.Labels,
            Permissions = board.Permissions,
            Members = members,
            Columns = board.Columns.Select(column => new BoardColumnResponse
                               {
                                   Key = column.Key,
                                   Name = column.Name,
                                   Cards = column.Key == BoardColumnKey.Done
                                       ? column.Cards.OrderByDescending(c => c.ClosedAt).Take(20).Select(MapCard).ToList()
                                       : column.Cards.OrderBy(c => c.Order).Select(MapCard).ToList(),
                                   TotalCards = column.Cards.Count
                               }
                           )
                           .ToList()
        };
    }

    [HttpPost]
    [Permissions(Permissions.Admin)]
    public async Task<BoardListItem> CreateBoard([FromBody] CreateBoardRequest request)
    {
        var board = await boardService.CreateBoard(request.Name, request.Permissions);
        return new BoardListItem
        {
            Id = board.Id,
            Name = board.Name,
            MemberCount = boardService.ResolveAccessMembers(board.Permissions).Count
        };
    }

    [HttpPut("{boardId}")]
    [Permissions(Permissions.Admin)]
    public async Task UpdateBoard([FromRoute] string boardId, [FromBody] UpdateBoardRequest request)
    {
        await boardService.UpdateBoard(boardId, request.Name, request.Permissions, request.Labels);
        await boardHub.Clients.Group(boardId).ReceiveBoardUpdated(new { boardId });
    }

    [HttpDelete("{boardId}")]
    [Permissions(Permissions.Admin)]
    public async Task DeleteBoard([FromRoute] string boardId)
    {
        await boardService.SoftDeleteBoard(boardId);
    }

    [HttpPost("{boardId}/cards")]
    [Authorize]
    public async Task<BoardCardResponse> CreateCard([FromRoute] string boardId, [FromBody] CreateCardRequest request)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        var card = await boardService.CreateCard(boardId, request.Title);
        var response = MapCard(card);

        await boardHub.Clients.Group(boardId).ReceiveCardCreated(response);
        return response;
    }

    [HttpPut("{boardId}/cards/{cardId}")]
    [Authorize]
    public async Task<BoardCardResponse> UpdateCard([FromRoute] string boardId, [FromRoute] string cardId, [FromBody] UpdateCardRequest request)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        await boardService.UpdateCard(boardId, cardId, request.Title, request.Detail, request.Labels, request.AssigneeId);

        var updatedBoard = boardService.GetBoard(boardId);
        var card = updatedBoard.Columns.SelectMany(c => c.Cards).First(c => c.Id == cardId);
        var response = MapCard(card);

        await boardHub.Clients.Group(boardId).ReceiveCardUpdated(response);
        return response;
    }

    [HttpDelete("{boardId}/cards/{cardId}")]
    [Authorize]
    public async Task DeleteCard([FromRoute] string boardId, [FromRoute] string cardId)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        await boardService.DeleteCard(boardId, cardId);
        await boardHub.Clients.Group(boardId).ReceiveCardDeleted(new { cardId });
    }

    [HttpPut("{boardId}/cards/{cardId}/move")]
    [Authorize]
    public async Task<BoardCardResponse> MoveCard([FromRoute] string boardId, [FromRoute] string cardId, [FromBody] MoveCardRequest request)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        var (card, _, targetColumnCards) = await boardService.MoveCard(boardId, cardId, request.TargetColumn, request.TargetIndex);
        var response = MapCard(card);

        await boardHub.Clients.Group(boardId)
        .ReceiveCardMoved(
            new
            {
                Card = response,
                TargetColumn = request.TargetColumn,
                TargetColumnCards = targetColumnCards.Select(MapCard).ToList()
            }
        );

        return response;
    }

    [HttpGet("{boardId}/done")]
    [Authorize]
    public IEnumerable<BoardCardResponse> GetDoneCards([FromRoute] string boardId, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        return boardService.GetDoneCards(boardId, skip, take).Select(MapCard);
    }

    [HttpPost("{boardId}/cards/{cardId}/commentthread")]
    [Authorize]
    public async Task<string> EnsureCommentThread([FromRoute] string boardId, [FromRoute] string cardId)
    {
        var board = boardService.GetBoard(boardId);
        RequireAccess(board);

        return await boardService.EnsureCommentThread(boardId, cardId);
    }

    private void RequireAccess(DomainBoard board)
    {
        if (board is null || board.IsDeleted || !boardService.HasAccess(board))
        {
            throw new UnauthorizedAccessException("You do not have access to this board");
        }
    }

    private BoardCardResponse MapCard(BoardCard card)
    {
        return new BoardCardResponse
        {
            Id = card.Id,
            Title = card.Title,
            Detail = card.Detail,
            Labels = card.Labels,
            AssigneeId = card.AssigneeId,
            AssigneeName = string.IsNullOrEmpty(card.AssigneeId) ? null : displayNameService.GetDisplayName(card.AssigneeId),
            CreatedBy = card.CreatedBy,
            CreatedByName = displayNameService.GetDisplayName(card.CreatedBy),
            CreatedAt = card.CreatedAt,
            ClosedAt = card.ClosedAt,
            Order = card.Order,
            CommentThreadId = card.CommentThreadId,
            Activity = card.Activity.Select(a => new BoardCardActivityResponse
                               {
                                   UserName = displayNameService.GetDisplayName(a.UserId),
                                   Timestamp = a.Timestamp,
                                   Description = a.Description
                               }
                           )
                           .ToList()
        };
    }
}

public class BoardListItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int MemberCount { get; set; }
}

public class BoardResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<string> Labels { get; set; }
    public BoardPermissions Permissions { get; set; }
    public List<BasicAccount> Members { get; set; }
    public List<BoardColumnResponse> Columns { get; set; }
}

public class BoardColumnResponse
{
    public BoardColumnKey Key { get; set; }
    public string Name { get; set; }
    public List<BoardCardResponse> Cards { get; set; }
    public int TotalCards { get; set; }
}

public class BoardCardResponse
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
    public List<string> Labels { get; set; }
    public string AssigneeId { get; set; }
    public string AssigneeName { get; set; }
    public string CreatedBy { get; set; }
    public string CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int Order { get; set; }
    public string CommentThreadId { get; set; }
    public List<BoardCardActivityResponse> Activity { get; set; }
}

public class BoardCardActivityResponse
{
    public string UserName { get; set; }
    public DateTime Timestamp { get; set; }
    public string Description { get; set; }
}

public class CreateBoardRequest
{
    public string Name { get; set; }
    public BoardPermissions Permissions { get; set; }
}

public class UpdateBoardRequest
{
    public string Name { get; set; }
    public BoardPermissions Permissions { get; set; }
    public List<string> Labels { get; set; }
}

public class CreateCardRequest
{
    public string Title { get; set; }
}

public class UpdateCardRequest
{
    public string Title { get; set; }
    public string Detail { get; set; }
    public List<string> Labels { get; set; }
    public string AssigneeId { get; set; }
}

public class MoveCardRequest
{
    public BoardColumnKey TargetColumn { get; set; }
    public int TargetIndex { get; set; }
}
