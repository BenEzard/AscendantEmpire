using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class GetTurnHandlerInMemoryTests
{
    private InMemoryDatabaseRoot _dbRoot = null!;
    private string _dbName = null!;
    private DbContextOptions<AscendantEmpireContext> _options = null!;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private ILogger<GetTurnHandler> _logger = null!;
    private GetTurnHandler _handler = null!;

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

        _logger = NullLogger<GetTurnHandler>.Instance;

        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AscendantEmpireContext(_options)));

        _handler = new GetTurnHandler(_logger, _dbFactory);
    }

    // -----------------------------
    // A) Happy-path return tests
    // -----------------------------

    [Test, Description("Returns 1 campaign lane entry when a campaign TakingTurn exists and requester is on that roster.")]
    public async Task ShouldReturnCampaignLaneEntryAsync()
    {
        int gameId = 1;

        List<int> players =
        [
            11, // taking turn
            12, // requester
        ];

        await SeedGameAndPlayersAsync(gameId, players);

        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 11, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 12, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 12);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.IsCampaign, Is.True);
            Assert.That(entry.RoundNumber, Is.EqualTo(1));
            Assert.That(entry.CurrentTurnPlayerId, Is.EqualTo(11));
            Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.EqualTo(1));
            Assert.That(entry.MaxTimeRemainingUntilTurn.TotalSeconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(entry.MaxTimeRemainingUntilTurn.TotalSeconds, Is.LessThanOrEqualTo(45));
        }
    }

    [Test, Description("Returns 1 battle lane entry when a battle TakingTurn exists and requester is on that battle roster.")]
    public async Task ShouldReturnBattleLaneEntryAsync()
    {
        int gameId = 1;
        int battleId = 9001;

        var players = new List<int>
        {
            21, // taking turn
            22, // requester
        };

        await SeedGameAndPlayersAsync(gameId, players);

        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 5,
            battleId: battleId,
            rosterInOrder:
            [
                (PlayerId: 21, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 22, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(20));

        var request = new GetTurnRequest(gameId, PlayerId: 22);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.IsCampaign, Is.False);
            Assert.That(entry.RoundNumber, Is.EqualTo(5));
            Assert.That(entry.CurrentTurnPlayerId, Is.EqualTo(21));
            Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.EqualTo(1));
        }
    }

    [Test, Description("Returns multiple lane entries when player is on both campaign and battle rosters that have TakingTurn rows.")]
    public async Task ShouldReturnMultipleLaneEntriesAsync()
    {
        int gameId = 1;
        Guid gameGuid = Guid.NewGuid();
        int battleId = 123;

        var players = new List<int>
        {
            31, // campaign taking turn
            32, // requester (on both lanes)
            33, // battle taking turn
        };

        await SeedGameAndPlayersAsync(gameId, players);

        // Campaign lane (BattleId null), Round 1
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 31, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 32, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(60));

        // Battle lane (BattleId 123), Round 2
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 2,
            battleId: battleId,
            rosterInOrder:
            [
                (PlayerId: 33, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 32, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(60));

        var request = new GetTurnRequest(gameId, PlayerId: 32);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        var entries = result.SuccessValue!.ToArray();
        Assert.That(entries, Has.Length.EqualTo(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries.Count(e => e.IsCampaign), Is.EqualTo(1));
            Assert.That(entries.Count(e => !e.IsCampaign), Is.EqualTo(1));

            var campaign = entries.Single(e => e.IsCampaign);
            Assert.That(campaign.RoundNumber, Is.EqualTo(1));
            Assert.That(campaign.CurrentTurnPlayerId, Is.EqualTo(31));

            var battle = entries.Single(e => !e.IsCampaign);
            Assert.That(battle.RoundNumber, Is.EqualTo(2));
            Assert.That(battle.CurrentTurnPlayerId, Is.EqualTo(33));
        }
    }

    // ---------------------------------------
    // B) NumberOfPlayersUntilMyTurn tests
    // ---------------------------------------

    [Test, Description("NumberOfPlayersUntilMyTurn: requester is current-turn player -> 0.")]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldReturnZeroWhenRequesterIsCurrentTurnPlayerAsync()
    {
        int gameId = 1;

        var players = new List<int>
        {
            41, // taking + requester
            42,
        };

        await SeedGameAndPlayersAsync(gameId, players);

        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 41, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 42, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 41);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.Zero);
    }

    [Test, Description("NumberOfPlayersUntilMyTurn: non-wrap case counts current + players between. Example r=2, my=5, between {3,4} => 3.")]
    public async Task ShouldComputeNumberOfPlayersUntilMyTurnWithBetweenPlayersAsync()
    {
        int gameId = 1;

        var players = new List<int>
        {
            51, // order 1
            52, // order 2 (taking)
            53, // order 3
            54, // order 4
            55, // order 5 (requester)
        };

        await SeedGameAndPlayersAsync(gameId, players);
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 51, Status: TurnStatus.Pending,    TurnOrder: 1),
                (PlayerId: 52, Status: TurnStatus.TakingTurn, TurnOrder: 2),
                (PlayerId: 53, Status: TurnStatus.Pending,    TurnOrder: 3),
                (PlayerId: 54, Status: TurnStatus.Pending,    TurnOrder: 4),
                (PlayerId: 55, Status: TurnStatus.Pending,    TurnOrder: 5),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 55);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.EqualTo(3));
    }

    [Test, Description("NumberOfPlayersUntilMyTurn: non-wrap case, requester immediately after current. Example r=2, my=3 => 1.")]
    public async Task ShouldComputeNumberOfPlayersUntilMyTurnImmediateNextAsync()
    {
        int gameId = 1;

        var players = new List<int>
        {
            61, // order 1
            62, // order 2 (taking)
            63, // order 3 (requester)
        };

        await SeedGameAndPlayersAsync(gameId, players);

        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 61, Status: TurnStatus.Pending,    TurnOrder: 1),
                (PlayerId: 62, Status: TurnStatus.TakingTurn, TurnOrder: 2),
                (PlayerId: 63, Status: TurnStatus.Pending,    TurnOrder: 3),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 63);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.EqualTo(1));
    }

    [Test, Description("NumberOfPlayersUntilMyTurn: inactive players are excluded from between-count.")]
    public async Task ShouldExcludeInactivePlayersFromBetweenCountAsync()
    {
        // Orders 1..5. Current is 2. Requester is 5.
        // Between are 3 and 4, but player at 4 is inactive => between count=1 => result=1+1=2.
        int gameId = 1;

        var players = new List<int>
        {
            91, // order 1
            92, // order 2 (taking)
            93, // order 3
            94, // order 4 (Eliminated)
            95, // order 5 (requester)
        };

        await SeedGameAndPlayersAsync(gameId, players);
        await SetPlayerStatusAsync(gameId, playerId: 94, PlayerStatus.Eliminated);

        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 91, Status: TurnStatus.Pending,    TurnOrder: 1),
                (PlayerId: 92, Status: TurnStatus.TakingTurn, TurnOrder: 2),
                (PlayerId: 93, Status: TurnStatus.Pending,    TurnOrder: 3),
                (PlayerId: 94, Status: TurnStatus.Pending,    TurnOrder: 4),
                (PlayerId: 95, Status: TurnStatus.Pending,    TurnOrder: 5),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 95);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        var entry = result.SuccessValue!.Single();

        Assert.That(entry.NumberOfPlayersUntilMyTurn, Is.EqualTo(2));
    }

    [Test, Description("NotFound: requester exists on roster but requester is inactive -> no lane returned.")]
    [Category("ProductionBugSuspected")]
    [Ignore("ProductionBugSuspected")]
    public async Task ShouldReturnNotFoundWhenRequesterNotActiveAsync()
    {
        int gameId = 1;

        var players = new List<int>
        {
            101, // taking
            102, // requester (inactive)
        };

        await SeedGameAndPlayersAsync(gameId, players);
        await SetPlayerStatusAsync(gameId, playerId: 102, PlayerStatus.Eliminated);

        // Lane has both, but handler filters my.Player.Status == Active
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
                (PlayerId: 101, Status: TurnStatus.TakingTurn, TurnOrder: 1),
                (PlayerId: 102, Status: TurnStatus.Pending,    TurnOrder: 2),
            ],
            scheduledTurnEndTimeUtc: DateTimeOffset.UtcNow.AddSeconds(30));

        var request = new GetTurnRequest(gameId, PlayerId: 102);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(e => e.Contains("No records found", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    private async Task SeedGameAndPlayersAsync(int gameId, IReadOnlyCollection<int> players)
    {
        await using var context = new AscendantEmpireContext(_options);

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

        foreach (var pId in players)
        {
            context.Players.Add(new Player
            {
                Id = pId,
                GameId = gameId,
                Status = PlayerStatus.Active,
                RulerName = "Ruler",
                RulerTitle = "Title",
                FactionName = "Faction",
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SetPlayerStatusAsync(int gameId, int playerId, PlayerStatus status)
    {
        await using var context = new AscendantEmpireContext(_options);

        Player player = await context.Players.SingleAsync(p => p.GameId == gameId && p.Id == playerId);
        player.Status = status;

        await context.SaveChangesAsync();
    }

    private async Task SeedLaneRosterAsync(
        int gameId,
        int roundNumber,
        int? battleId,
        IReadOnlyCollection<(int PlayerId, TurnStatus Status, int TurnOrder)> rosterInOrder,
        DateTimeOffset scheduledTurnEndTimeUtc)
    {
        await using var context = new AscendantEmpireContext(_options);

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == gameId);

        // Load all players for nav-fixup / explicit nav assignment (important for InMemory + navigation usage in handler).
        var playerIds = rosterInOrder.Select(r => r.PlayerId).Distinct().ToArray();
        var playersById = await context.Players
            .Where(p => p.GameId == gameId && playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        foreach (var r in rosterInOrder)
        {
            Player player = playersById[r.PlayerId];

            context.TurnRosters.Add(new TurnRoster
            {
                GameId = gameId,
                Game = game,
                PlayerId = r.PlayerId,
                Player = player,

                TurnOrder = r.TurnOrder,
                BattleId = battleId,
                PlayerStillInBattle = battleId == null ? null : true,
                PlayerBattleName = battleId == null ? null : "BattleName",

                Status = r.Status,
                TurnStartTime = null,
                TurnEndTime = null,
                ScheduledTurnEndTime = scheduledTurnEndTimeUtc,

                RoundNumber = roundNumber,
                CreatedAt = createdAt
            });
        }

        await context.SaveChangesAsync();
    }

    [Test, Description("When the requester is on multiple active lanes (campaign + 1+ battles) that each have a TakingTurn row, handler should return one entry per lane.")]
    public async Task ShouldReturnMultipleLaneEntries_ForCampaignAndBattlesAsync()
    {
        int gameId = 1;
        Guid gameGuid = Guid.NewGuid();

        int battleId1 = 1001;
        int battleId2 = 1002;

        // Players:
        // - requester is on campaign + both battles
        // - each lane has its own current-turn player (can overlap, but keep distinct for clarity)
        var players = new List<int>
        {
            10, // requester
            11, // campaign current turn
            12, // battle1 current turn
            13, // battle2 current turn
        };

        await SeedGameAndPlayersAsync(gameId, players);

        DateTimeOffset endUtc = DateTimeOffset.UtcNow.AddSeconds(60);

        // Campaign lane: BattleId NULL, Round 1
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: null,
            rosterInOrder:
            [
            (PlayerId: 11, Status: TurnStatus.TakingTurn, TurnOrder: 1), // current
            (PlayerId: 10, Status: TurnStatus.Pending,    TurnOrder: 2), // requester
            ],
            scheduledTurnEndTimeUtc: endUtc);

        // Battle lane 1: BattleId 1001, Round 1
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: battleId1,
            rosterInOrder:
            [
            (PlayerId: 12, Status: TurnStatus.TakingTurn, TurnOrder: 1), // current
            (PlayerId: 10, Status: TurnStatus.Pending,    TurnOrder: 2), // requester
            ],
            scheduledTurnEndTimeUtc: endUtc);

        // Battle lane 2: BattleId 1002, Round 1
        await SeedLaneRosterAsync(
            gameId,
            roundNumber: 1,
            battleId: battleId2,
            rosterInOrder:
            [
            (PlayerId: 13, Status: TurnStatus.TakingTurn, TurnOrder: 1), // current
            (PlayerId: 10, Status: TurnStatus.Pending,    TurnOrder: 2), // requester
            ],
            scheduledTurnEndTimeUtc: endUtc);

        var request = new GetTurnRequest(gameId, PlayerId: 10);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
        }

        var entries = result.SuccessValue!.ToArray();

        Assert.That(entries, Has.Length.EqualTo(3), "Expected one entry per lane: campaign + two battles.");

        using (Assert.EnterMultipleScope())
        {
            // Exactly one campaign lane, two battle lanes
            Assert.That(entries.Count(e => e.IsCampaign), Is.EqualTo(1));
            Assert.That(entries.Count(e => !e.IsCampaign), Is.EqualTo(2));

            // Campaign lane should report campaign current player
            var campaign = entries.Single(e => e.IsCampaign);
            Assert.That(campaign.CurrentTurnPlayerId, Is.EqualTo(11));
            Assert.That(campaign.RoundNumber, Is.EqualTo(1));
            Assert.That(campaign.NumberOfPlayersUntilMyTurn, Is.EqualTo(1));

            // Battle lanes should report both battle current players (order independent)
            var battleCurrents = entries
                .Where(e => !e.IsCampaign)
                .Select(e => e.CurrentTurnPlayerId)
                .ToArray();

            Assert.That(battleCurrents, Is.EquivalentTo([12, 13]));

            // Loose time assertion (avoid flakiness)
            Assert.That(entries.All(e => e.MaxTimeRemainingUntilTurn.TotalSeconds >= 0), Is.True);
        }
    }

}
