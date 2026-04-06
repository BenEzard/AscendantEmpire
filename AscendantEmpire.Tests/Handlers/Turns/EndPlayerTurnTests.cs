using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class EndPlayerTurnHandlerTests
{
    private InMemoryDatabaseRoot _dbRoot = null!;
    private string _dbName = null!;
    private DbContextOptions<AscendantEmpireContext> _options = null!;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private IMediator _mediator = null!;
    private ILogger<EndPlayerTurnHandler> _logger = null!;
    private EndPlayerTurnHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbRoot = new InMemoryDatabaseRoot();
        _dbName = Guid.NewGuid().ToString();

        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(_dbName, _dbRoot)
            .Options;

        await using (var setupContext = new AscendantEmpireContext(_options))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _logger = NullLogger<EndPlayerTurnHandler>.Instance;

        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AscendantEmpireContext(_options)));

        _mediator = Substitute.For<IMediator>();

        // Safe default for the awaited EndTurnOnRosterRequest (override per-test)
        _mediator.Send(Arg.Any<EndTurnOnRosterRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Succeed()));

        _handler = new EndPlayerTurnHandler(_logger, _mediator, _dbFactory);
    }

    [Test]
    public async Task ShouldFail_WhenValidationFailsAsync()
    {
        var request = new EndPlayerTurnRequest(GameId: 0, PlayerId: 0);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Any(e => e.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(GetTurnRequest)!, default);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenGetTurnFailsAsync()
    {
        int gameId = 404;
        int playerId = 404;

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId  && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Fail(["turn lookup failed"])));

        var request = new EndPlayerTurnRequest(gameId, playerId);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Failed to retrieve current turn information", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenTurnResultEmptyAsync()
    {
        int gameId = 404;
        int playerId = 404;

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed([])));

        var request = new EndPlayerTurnRequest(gameId, playerId);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Player not found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenNoCampaignTurnEntryAsync()
    {
        int gameId = 404;
        int playerId = 404;

        var entries = new List<RosterEntry>
        {
            new() {
                IsCampaign = false,
                RoundNumber = 1,
                CurrentTurnPlayerId = 123,
                NumberOfPlayersUntilMyTurn = 0
            }
        };

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(entries)));

        var request = new EndPlayerTurnRequest(gameId, playerId);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("No current turn information found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenNotPlayersTurnAsync()
    {
        int gameId = 404;
        int playerId = 404;

        var entries = new List<RosterEntry>
        {
            new() {
                IsCampaign = true,
                RoundNumber = 1,
                CurrentTurnPlayerId = 11,
                NumberOfPlayersUntilMyTurn = 2
            }
        };

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(entries)));

        var request = new EndPlayerTurnRequest(gameId, playerId);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("not the specified Player's turn", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldFail_WhenGameNotFoundAsync()
    {
        int gameId = 404;
        int playerId = 404;

        var entries = new List<RosterEntry>
        {
            new() {
                IsCampaign = true,
                RoundNumber = 1,
                CurrentTurnPlayerId = 11,
                NumberOfPlayersUntilMyTurn = 0
            }
        };

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(entries)));

        // Intentionally do NOT seed GameIndex

        var request = new EndPlayerTurnRequest(gameId, playerId);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("Game not found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.DidNotReceiveWithAnyArgs().Send(default(EndTurnOnRosterRequest)!, default);
    }

    [Test]
    public async Task ShouldSucceed_AndSendEndTurnOnRosterRequestAsync()
    {
        int gameId = 1;
        int playerId = 42;
        int roundNumber = 7;
        int currentTurnPlayerId = 42;

        await SeedGameAsync(gameId, numberOfPlayers: 2);

        var entries = new List<RosterEntry>
        {
            new() {
                IsCampaign = true,
                RoundNumber = roundNumber,
                CurrentTurnPlayerId = currentTurnPlayerId,
                NumberOfPlayersUntilMyTurn = 0
            }
        };

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(entries)));

        _mediator.Send(
                Arg.Is<EndTurnOnRosterRequest>(r =>
                    r.GameId == gameId
                    && r.RoundNumber == roundNumber
                    && r.CurrentTurnPlayerId == currentTurnPlayerId
                    && r.BattleId == null),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Succeed()));

        var request = new EndPlayerTurnRequest(gameId, playerId);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        await _mediator.Received(1).Send(
            Arg.Is<EndTurnOnRosterRequest>(r =>
                r.GameId == gameId
                && r.RoundNumber == roundNumber
                && r.CurrentTurnPlayerId == currentTurnPlayerId
                && r.BattleId == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ShouldFail_WhenEndTurnOnRosterFailsAsync()
    {

        int gameId = 1;
        int playerId = 404;
        int roundNumber = 3;
        int currentTurnPlayerId = 11;

        await SeedGameAsync(gameId, numberOfPlayers: 2);

        var entries = new List<RosterEntry>
        {
            new() {
                IsCampaign = true,
                RoundNumber = roundNumber,
                CurrentTurnPlayerId = currentTurnPlayerId,
                NumberOfPlayersUntilMyTurn = 0
            }
        };

        _mediator.Send(
                Arg.Is<GetTurnRequest>(r => r.GameId == gameId && r.PlayerId == playerId),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(entries)));

        _mediator.Send(
                Arg.Any<EndTurnOnRosterRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Fail(["db update failed"])));

        var request = new EndPlayerTurnRequest(gameId, playerId);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Any(e => e.Contains("Failed to end turn for player", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        await _mediator.Received(1).Send(
            Arg.Is<EndTurnOnRosterRequest>(r =>
                r.GameId == gameId
                && r.RoundNumber == roundNumber
                && r.CurrentTurnPlayerId == currentTurnPlayerId
                && r.BattleId == null),
            Arg.Any<CancellationToken>());
    }

    private async Task SeedGameAsync(int gameId, int numberOfPlayers)
    {
        await using var context = new AscendantEmpireContext(_options);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = "Test Game",
            NumberOfPlayers = numberOfPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress
        });

        await context.SaveChangesAsync();
    }
}
