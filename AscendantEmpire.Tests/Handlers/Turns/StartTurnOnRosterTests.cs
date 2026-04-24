using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class StartTurnOnRosterTests
{
    private static readonly ServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(StartTurnOnRosterTests), DbRoot)
        .UseInternalServiceProvider(EfServiceProvider)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private ILogger<StartTurnOnRosterHandler> _logger = null!;
    private StartTurnOnRosterHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        // Create database fresh for each test
        await using (var setupContext = new AscendantEmpireContext(DbOptions))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        // IMPORTANT: don't NSubstitute ILogger<T> (your setup can't proxy it); use a real no-op logger
        _logger = NullLogger<StartTurnOnRosterHandler>.Instance;

        // DbContext factory that returns NEW contexts sharing the same in-memory database
        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AscendantEmpireContext(DbOptions)));

        _handler = new StartTurnOnRosterHandler(_logger, _dbFactory);
    }

    [Test, Description("When request fails validation, handler should return Fail with validation errors and not update anything.")]
    public async Task ShouldFail_WhenRequestInvalidAsync()
    {
        var request = new StartTurnOnRosterRequest(
            GameId: 0,
            PlayerId: 0,
            RoundNumber: 1);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Expected validation to fail.");
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors.Any(e => e.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test, Description("When player is not found in the specified game (or not active), handler should return Fail.")]
    public async Task ShouldFail_WhenPlayerNotFoundOrNotActiveAsync()
    {
        int gameId = 1;

        // Seed game, but no matching active player for request
        await SeedGameAsync(gameId);

        var request = new StartTurnOnRosterRequest(
            GameId: gameId,
            PlayerId: 404, // doesn't exist
            RoundNumber: 1);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
        }
        Assert.That(result.Errors.Any(e => e.Contains("Player not found", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test, Description("When player exists/active but no TurnRoster row exists for that player+round, handler should return Fail.")]
    public async Task ShouldFail_WhenTurnRosterNotFoundForPlayerAndRoundAsync()
    {
        int gameId = 1;

        int playerId = 11;

        await SeedGameAndActivePlayersAsync(gameId, [playerId]);

        // NOTE: no roster seeded

        var request = new StartTurnOnRosterRequest(
            GameId: gameId,
            PlayerId: playerId,
            RoundNumber: 1);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
        }
        Assert.That(result.Errors.Any(e => e.Contains("Turn Roster entry not found", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test, Description("When player exists/active and TurnRoster row exists, handler should set TakingTurn and set start/scheduled end times.")]
    public async Task ShouldSucceed_AndUpdateRosterAsync()
    {
        int gameId = 1;

        int playerId = 21;

        int roundNumber = 3;

        await SeedGameAndActivePlayersAsync(gameId, [playerId]);

        int rosterId = await SeedRosterAsync(
            gameId: gameId,
            roundNumber: roundNumber,
            playerId: playerId,
            turnOrder: 1,
            status: TurnStatus.Pending);

        var request = new StartTurnOnRosterRequest(
            GameId: gameId,
            PlayerId: playerId,
            RoundNumber: roundNumber);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, "Expected handler to succeed.");

        await using var assertContext = new AscendantEmpireContext(DbOptions);

        TurnRoster updated = await assertContext.TurnRosters
            .AsNoTracking()
            .SingleAsync(r => r.Id == rosterId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated.Status, Is.EqualTo(TurnStatus.TakingTurn), "Roster status should be TakingTurn.");
            Assert.That(updated.TurnStartTime, Is.Not.Null, "TurnStartTime should be set.");
            Assert.That(updated.ScheduledTurnEndTime, Is.Not.Null, "ScheduledTurnEndTime should be set.");

            Assert.That(updated.ScheduledTurnEndTime, Is.GreaterThan(updated.TurnStartTime), "Scheduled end should be after start.");

            // Handler sets ScheduledTurnEndTime = Now.AddHours(48). Because it calls Now twice,
            // allow a small tolerance window around 48 hours.
            TimeSpan delta = updated.ScheduledTurnEndTime!.Value - updated.TurnStartTime!.Value;
            Assert.That(delta, Is.InRange(TimeSpan.FromHours(47.95), TimeSpan.FromHours(48.05)),
                $"Expected scheduled end about 48 hours after start; got {delta.TotalHours:F4} hours.");
        }
    }

    [Test, Description("Only the matching TurnRoster row for the requested round should be updated; other rounds/players should remain unchanged.")]
    public async Task ShouldOnlyUpdateMatchingRosterRowAsync()
    {
        int gameId = 1;

        var players = new List<int>
        {
            31,
            32,
        };

        await SeedGameAndActivePlayersAsync(gameId, players);

        int targetRound = 2;

        // Seed rosters:
        // - Target player in target round: Pending (should update)
        // - Same player in different round: Pending (should not update)
        // - Other player in target round: Pending (should not update)
        int targetRosterId = await SeedRosterAsync(gameId, targetRound, players[0], turnOrder: 1, status: TurnStatus.Pending);
        int otherRoundSamePlayerId = await SeedRosterAsync(gameId, roundNumber: 1, players[0], turnOrder: 1, status: TurnStatus.Pending);
        int otherPlayerSameRoundId = await SeedRosterAsync(gameId, targetRound, players[1], turnOrder: 2, status: TurnStatus.Pending);

        var request = new StartTurnOnRosterRequest(
            GameId: gameId,
            PlayerId: players[0],
            RoundNumber: targetRound);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        await using var assertContext = new AscendantEmpireContext(DbOptions);

        TurnRoster target = await assertContext.TurnRosters.AsNoTracking().SingleAsync(r => r.Id == targetRosterId);
        TurnRoster samePlayerOtherRound = await assertContext.TurnRosters.AsNoTracking().SingleAsync(r => r.Id == otherRoundSamePlayerId);
        TurnRoster otherPlayerSameRound = await assertContext.TurnRosters.AsNoTracking().SingleAsync(r => r.Id == otherPlayerSameRoundId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.Status, Is.EqualTo(TurnStatus.TakingTurn));
            Assert.That(target.TurnStartTime, Is.Not.Null);
            Assert.That(target.ScheduledTurnEndTime, Is.Not.Null);

            Assert.That(samePlayerOtherRound.Status, Is.EqualTo(TurnStatus.Pending), "Other round should remain unchanged.");
            Assert.That(samePlayerOtherRound.TurnStartTime, Is.Null, "Other round should remain unchanged.");
            Assert.That(samePlayerOtherRound.ScheduledTurnEndTime, Is.Null, "Other round should remain unchanged.");

            Assert.That(otherPlayerSameRound.Status, Is.EqualTo(TurnStatus.Pending), "Other player should remain unchanged.");
            Assert.That(otherPlayerSameRound.TurnStartTime, Is.Null, "Other player should remain unchanged.");
            Assert.That(otherPlayerSameRound.ScheduledTurnEndTime, Is.Null, "Other player should remain unchanged.");
        }
    }

    private async Task SeedGameAsync(int gameId)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = "Test Game",
            NumberOfPlayers = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedGameAndActivePlayersAsync(int gameId, IReadOnlyCollection<int> players)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        var game = new GameIndex
        {
            Id = gameId,
            GameName = "Test Game",
            NumberOfPlayers = players.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress
        };

        context.GameIndex.Add(game);

        foreach (var playerId in players)
        {
            context.Players.Add(new Player
            {
                Id = playerId,
                GameId = gameId,
                Game = game,
                Status = PlayerStatus.Active,
                RulerName = "Ruler",
                RulerTitle = "Title",
                FactionName = "Faction",
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<int> SeedRosterAsync(
        int gameId,
        int roundNumber,
        int playerId,
        int turnOrder,
        TurnStatus status)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        var roster = new TurnRoster
        {
            GameId = gameId,
            PlayerId = playerId,
            TurnOrder = turnOrder,
            BattleId = null,
            PlayerStillInBattle = null,
            PlayerBattleName = null,
            Status = status,
            TurnStartTime = null,
            TurnEndTime = null,
            ScheduledTurnEndTime = null,
            RoundNumber = roundNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.TurnRosters.Add(roster);
        await context.SaveChangesAsync();

        return roster.Id;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        EfServiceProvider.Dispose();
    }
}
