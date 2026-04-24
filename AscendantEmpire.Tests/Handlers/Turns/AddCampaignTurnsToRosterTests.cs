using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using NSubstitute;
using Microsoft.EntityFrameworkCore.Storage;
using AscendantEmpire.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AscendantEmpire.Application.Common;
using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;
using Microsoft.Extensions.Logging.Abstractions;
using AscendantEmpire.Application.Features.Turns.Handlers;

namespace AscendantEmpire.Tests.Handlers.Turns;

internal class AddCampaignTurnsToRosterTests
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(AddCampaignTurnsToRosterTests), DbRoot)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private readonly ILogger<AddCampaignTurnsToRosterHandler> _logger = null!;
    private AddCampaignTurnsToRosterHandler _handler = null!;

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
        ILogger<AddCampaignTurnsToRosterHandler> logger = NullLogger<AddCampaignTurnsToRosterHandler>.Instance;

        // DbContext factory that returns NEW contexts sharing the same in-memory database
        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _dbFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new AscendantEmpireContext(DbOptions)));

        _handler = new AddCampaignTurnsToRosterHandler(_logger, _dbFactory);
    }

    [Test, Description("When no roster exists for the game, handler should create the first roster (Round 1) for all active players.")]
    public async Task ShouldCreateInitialRosterAsync()
    {
        Guid gameGuid = Guid.NewGuid();
        int gameId = 1;

        List<int> players = [11, 12, 13];

        await SeedGameAndActivePlayersAsync(gameId, players);

        var request = new AddCampaignTurnsToRosterRequest(gameId);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, "Handler should have succeeded creating initial roster.");

        await using var assertContext = new AscendantEmpireContext(DbOptions);

        List<TurnRoster> rosters = await assertContext.TurnRosters
            .AsNoTracking()
            .Where(r => r.GameId == gameId)
            .OrderBy(r => r.TurnOrder)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rosters, Has.Count.EqualTo(players.Count), "Expected one roster row per active player.");
            Assert.That(rosters.All(r => r.RoundNumber == 1), Is.True, "All roster rows should be Round 1.");
            Assert.That(rosters.All(r => r.BattleId == null), Is.True, "Campaign roster should have BattleId NULL.");
            Assert.That(rosters.All(r => r.Status == TurnStatus.Pending), Is.True, "All roster rows should be Pending.");
            Assert.That(rosters.Select(r => r.TurnOrder).Distinct().Count(), Is.EqualTo(players.Count), "TurnOrder values should be unique.");
            Assert.That(rosters.Select(r => r.TurnOrder), Is.EquivalentTo(Enumerable.Range(1, players.Count)), "TurnOrder should be 1..N.");

            // Each seeded player should appear exactly once (order is randomized in handler, so don't assert ordering).
            Assert.That(rosters.Select(r => r.PlayerId), Is.EquivalentTo(players));
        }
    }

    [Test, Description("When a roster already exists, handler should duplicate the last campaign round roster into the next round, keeping TurnOrder the same.")]
    public async Task ShouldDuplicateRosterToNextRoundWhenExistingAsync()
    {
        int gameId = 1;

        List<int> players = [21, 22, 23];

        await SeedGameAndActivePlayersAsync(gameId, players);

        // Seed an existing campaign roster (BattleId NULL) for Round 1 in a known order
        await SeedExistingRosterAsync(
            gameId,
            roundNumber: 1,
            playerIdsInOrder: [21, 22, 23]);

        var request = new AddCampaignTurnsToRosterRequest(gameId);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, "Handler should have succeeded duplicating roster.");

        await using var assertContext = new AscendantEmpireContext(DbOptions);

        List<TurnRoster> allRosters = await assertContext.TurnRosters
            .AsNoTracking()
            .Where(r => r.GameId == gameId && r.BattleId == null)
            .OrderBy(r => r.RoundNumber)
            .ThenBy(r => r.TurnOrder)
            .ToListAsync();

        var round1 = allRosters.Where(r => r.RoundNumber == 1).ToList();
        var round2 = allRosters.Where(r => r.RoundNumber == 2).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(round1, Has.Count.EqualTo(players.Count), "Expected seeded Round 1 roster to exist.");
            Assert.That(round2, Has.Count.EqualTo(players.Count), "Expected Round 2 roster to be created.");

            Assert.That(round2.All(r => r.Status == TurnStatus.Pending), Is.True, "Round 2 roster rows should be Pending.");
            Assert.That(round2.All(r => r.BattleId == null), Is.True, "Round 2 roster should have BattleId NULL.");

            // TurnOrder should be preserved from Round 1 to Round 2, and map to same PlayerId per order.
            for (int i = 0; i < players.Count; i++)
            {
                Assert.That(round2[i].TurnOrder, Is.EqualTo(round1[i].TurnOrder), "TurnOrder should be preserved.");
                Assert.That(round2[i].PlayerId, Is.EqualTo(round1[i].PlayerId), "PlayerId should be preserved per TurnOrder.");
            }
        }
    }

    private async Task SeedGameAndActivePlayersAsync(
        int gameId,
        IReadOnlyCollection<int> players)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        context.GameIndex.Add(new GameIndex
        {
            Id = gameId,
            GameName = "Test Game",
            NumberOfPlayers = players.Count,
            CreatedAt = DateTimeOffset.UtcNow,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress
        });

        foreach (int playerId in players)
        {
            context.Players.Add(new Player
            {
                Id = playerId,
                GameId = gameId,
                Status = PlayerStatus.Active,
                RulerName = "Ruler",
                RulerTitle = "Title",
                FactionName = "Faction",
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedExistingRosterAsync(int gameId, int roundNumber, IReadOnlyList<int> playerIdsInOrder)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        for (int i = 0; i < playerIdsInOrder.Count; i++)
        {
            context.TurnRosters.Add(new TurnRoster
            {
                GameId = gameId,
                PlayerId = playerIdsInOrder[i],
                TurnOrder = i + 1,
                BattleId = null,
                PlayerStillInBattle = null,
                PlayerBattleName = null,
                Status = TurnStatus.Pending,
                TurnStartTime = null,
                TurnEndTime = null,
                ScheduledTurnEndTime = null,
                RoundNumber = roundNumber,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
