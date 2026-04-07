using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class BoardsControllerTests
{
    private readonly Mock<IBoardService> _mockBoardService = new();
    private readonly Mock<IHubContext<BoardHub, IBoardClient>> _mockBoardHub = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly Mock<IBoardClient> _mockBoardClient = new();
    private readonly BoardsController _controller;

    public BoardsControllerTests()
    {
        var mockClients = new Mock<IHubClients<IBoardClient>>();
        mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockBoardClient.Object);
        _mockBoardHub.Setup(x => x.Clients).Returns(mockClients.Object);

        _controller = new BoardsController(_mockBoardService.Object, _mockBoardHub.Object, _mockDisplayNameService.Object);
    }

    [Fact]
    public void GetBoards_should_return_accessible_boards()
    {
        var boards = new List<DomainBoard>
        {
            new()
            {
                Id = "board1",
                Name = "Board 1",
                Permissions = new BoardPermissions()
            },
            new()
            {
                Id = "board2",
                Name = "Board 2",
                Permissions = new BoardPermissions()
            }
        };
        _mockBoardService.Setup(x => x.GetAccessibleBoards()).Returns(boards);
        _mockBoardService.Setup(x => x.ResolveAccessMembers(It.IsAny<BoardPermissions>())).Returns([new BasicAccount { Id = "user1", DisplayName = "User 1" }]);

        var result = _controller.GetBoards().ToList();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("board1");
        result[0].Name.Should().Be("Board 1");
        result[0].MemberCount.Should().Be(1);
        result[1].Id.Should().Be("board2");
    }

    [Fact]
    public void GetBoard_should_return_board_with_columns()
    {
        var board = new DomainBoard
        {
            Id = "board1",
            Name = "Test Board",
            Labels = ["bug", "feature"],
            Permissions = new BoardPermissions(),
            Columns =
            [
                new BoardColumn
                {
                    Key = BoardColumnKey.Todo,
                    Name = "Todo",
                    Cards =
                    [
                        new BoardCard
                        {
                            Id = "card1",
                            Title = "Task 1",
                            CreatedBy = "user1",
                            CreatedAt = DateTime.UtcNow,
                            Order = 0,
                            Activity = []
                        }
                    ]
                },
                new BoardColumn
                {
                    Key = BoardColumnKey.Done,
                    Name = "Done",
                    Cards = []
                }
            ]
        };
        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);
        _mockBoardService.Setup(x => x.HasAccess(board)).Returns(true);
        _mockBoardService.Setup(x => x.ResolveAccessMembers(board.Permissions)).Returns([new BasicAccount { Id = "user1", DisplayName = "User 1" }]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName("user1")).Returns("User 1");

        var result = _controller.GetBoard("board1");

        result.Id.Should().Be("board1");
        result.Name.Should().Be("Test Board");
        result.Labels.Should().BeEquivalentTo(["bug", "feature"]);
        result.Members.Should().HaveCount(1);
        result.Columns.Should().HaveCount(2);
        result.Columns[0].Cards.Should().HaveCount(1);
        result.Columns[0].Cards[0].Title.Should().Be("Task 1");
    }

    [Fact]
    public void GetBoard_should_throw_when_no_access()
    {
        var board = new DomainBoard { Id = "board1", Permissions = new BoardPermissions() };
        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);
        _mockBoardService.Setup(x => x.HasAccess(board)).Returns(false);

        _controller.Invoking(c => c.GetBoard("board1")).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetBoard_should_throw_when_board_is_deleted()
    {
        var board = new DomainBoard
        {
            Id = "board1",
            IsDeleted = true,
            Permissions = new BoardPermissions()
        };
        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);

        _controller.Invoking(c => c.GetBoard("board1")).Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateCard_should_return_card_and_notify_hub()
    {
        var board = new DomainBoard { Id = "board1", Permissions = new BoardPermissions() };
        var card = new BoardCard
        {
            Id = "card1",
            Title = "New Card",
            CreatedBy = "user1",
            CreatedAt = DateTime.UtcNow,
            Order = 0,
            Activity =
            [
                new BoardCardActivity
                {
                    UserId = "user1",
                    Timestamp = DateTime.UtcNow,
                    Description = "created this card"
                }
            ]
        };

        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);
        _mockBoardService.Setup(x => x.HasAccess(board)).Returns(true);
        _mockBoardService.Setup(x => x.CreateCard("board1", "New Card")).ReturnsAsync(card);
        _mockDisplayNameService.Setup(x => x.GetDisplayName("user1")).Returns("User 1");

        var result = await _controller.CreateCard("board1", new CreateCardRequest { Title = "New Card" });

        result.Id.Should().Be("card1");
        result.Title.Should().Be("New Card");
        result.CreatedByName.Should().Be("User 1");
        _mockBoardClient.Verify(x => x.ReceiveCardCreated(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task MoveCard_should_return_card_and_notify_hub()
    {
        var board = new DomainBoard { Id = "board1", Permissions = new BoardPermissions() };
        var card = new BoardCard
        {
            Id = "card1",
            Title = "Card",
            CreatedBy = "user1",
            CreatedAt = DateTime.UtcNow,
            Order = 0,
            Activity = []
        };
        var targetColumnCards = new List<BoardCard> { card };

        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);
        _mockBoardService.Setup(x => x.HasAccess(board)).Returns(true);
        _mockBoardService.Setup(x => x.MoveCard("board1", "card1", BoardColumnKey.InProgress, 0))
                         .Returns(
                             Task.FromResult<(BoardCard Card, BoardColumnKey FromColumn, List<BoardCard> TargetColumnCards)>(
                                 (card, BoardColumnKey.Todo, targetColumnCards)
                             )
                         );
        _mockDisplayNameService.Setup(x => x.GetDisplayName("user1")).Returns("User 1");

        var result = await _controller.MoveCard("board1", "card1", new MoveCardRequest { TargetColumn = BoardColumnKey.InProgress, TargetIndex = 0 });

        result.Id.Should().Be("card1");
        _mockBoardClient.Verify(x => x.ReceiveCardMoved(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCard_should_notify_hub()
    {
        var board = new DomainBoard { Id = "board1", Permissions = new BoardPermissions() };
        _mockBoardService.Setup(x => x.GetBoard("board1")).Returns(board);
        _mockBoardService.Setup(x => x.HasAccess(board)).Returns(true);

        await _controller.DeleteCard("board1", "card1");

        _mockBoardService.Verify(x => x.DeleteCard("board1", "card1"), Times.Once);
        _mockBoardClient.Verify(x => x.ReceiveCardDeleted(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBoard_should_notify_hub()
    {
        var request = new UpdateBoardRequest
        {
            Name = "Updated",
            Color = "#2196f3",
            Permissions = new BoardPermissions(),
            Labels = ["label1"]
        };

        await _controller.UpdateBoard("board1", request);

        _mockBoardService.Verify(x => x.UpdateBoard("board1", "Updated", "#2196f3", request.Permissions, request.Labels), Times.Once);
        _mockBoardClient.Verify(x => x.ReceiveBoardUpdated(It.IsAny<object>()), Times.Once);
    }
}
