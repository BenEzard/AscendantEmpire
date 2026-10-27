using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using DescriptionAttribute = NUnit.Framework.DescriptionAttribute;

namespace AscendantEmpire.Tests.Handlers.Players;

internal class GetPlayersHandlerTests
{
    private static readonly ServiceProvider EfServiceProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(GetPlayersHandlerTests), DbRoot)
        .UseInternalServiceProvider(EfServiceProvider)
        .Options;

    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private ILogger<GetPlayersHandler> _logger = null!;
    private GetPlayersHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        await using (var setupContext = new AscendantEmpireContext(DbOptions))
        {
            await setupContext.Database.EnsureDeletedAsync();
            await setupContext.Database.EnsureCreatedAsync();
        }

        _logger = NullLogger<GetPlayersHandler>.Instance;
        _dbFactory = new TestDbContextFactory<AscendantEmpireContext>(DbOptions);
        _handler = new GetPlayersHandler(_logger, _dbFactory);
    }

    [Test, Description("When request fails validation, handler should return Fail with validation errors.")]
    public async Task ShouldFail_WhenRequestInvalidAsync()
    {
        var request = new GetPlayersRequest(
            GameId: 0,
            PlayerStatuses: null,
            PlayerIds: [0, -1]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors.Any(e => e.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test, Description("When no filters are supplied, handler should return all players for the specified game only.")]
    public async Task ShouldReturnAllPlayersForGameAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync(
            (Id: 11, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 101),
            (Id: 12, GameId: gameId, Status: PlayerStatus.Eliminated, CampaignOffsetId: 102),
            (Id: 21, GameId: 2, Status: PlayerStatus.Active, CampaignOffsetId: 201));

        var request = new GetPlayersRequest(gameId);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.Select(p => p.Id), Is.EquivalentTo(new[] { 11, 12 }));
        }
    }

    [Test, Description("When PlayerIds filter is supplied, handler should return only matching players in the specified game.")]
    public async Task ShouldFilterByPlayerIdsAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync([
            (Id: 11, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 101),
            (Id: 12, GameId: gameId, Status: PlayerStatus.Eliminated, CampaignOffsetId: 102),
            (Id: 13, GameId: gameId, Status: PlayerStatus.Banned, CampaignOffsetId: 103)
        ]);

        var request = new GetPlayersRequest(gameId, PlayerIds: [11, 13, 13]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.Select(p => p.Id), Is.EquivalentTo(new[] { 11, 13 }));
        }
    }

    [Test, Description("When PlayerStatuses filter is supplied, handler should return only players matching those statuses in the specified game.")]
    public async Task ShouldFilterByStatusesAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync([
            (Id: 11, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 101),
            (Id: 12, GameId: gameId, Status: PlayerStatus.Eliminated, CampaignOffsetId: 102),
            (Id: 13, GameId: gameId, Status: PlayerStatus.Banned, CampaignOffsetId: 103)
        ]);

        var request = new GetPlayersRequest(gameId, PlayerStatuses: [PlayerStatus.Active, PlayerStatus.Banned, PlayerStatus.Active]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.Select(p => p.Id), Is.EquivalentTo(new[] { 11, 13 }));
        }
    }

    [Test, Description("When both PlayerIds and PlayerStatuses filters are supplied, handler should apply both filters.")]
    public async Task ShouldFilterByIdsAndStatusesAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync(
            (Id: 11, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 101),
            (Id: 12, GameId: gameId, Status: PlayerStatus.Eliminated, CampaignOffsetId: 102),
            (Id: 13, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 103));

        var request = new GetPlayersRequest(
            GameId: gameId,
            PlayerStatuses: [PlayerStatus.Active],
            PlayerIds: [11, 12]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue!.Select(p => p.Id), Is.EquivalentTo(new[] { 11 }));
        }
    }

    [Test, Description("When no players match the supplied filters, handler should succeed and return an empty collection.")]
    public async Task ShouldReturnEmptyWhenNoPlayersMatchAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync(
            (Id: 11, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 101),
            (Id: 12, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 102));

        var request = new GetPlayersRequest(
            GameId: gameId,
            PlayerStatuses: [PlayerStatus.Eliminated],
            PlayerIds: [99]);

        var result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Not.Null);
            Assert.That(result.SuccessValue, Is.Empty);
        }
    }

    [Test, Description("Returned players should be projected into contract objects with the expected values.")]
    public async Task ShouldProjectPlayerContractFieldsAsync()
    {
        int gameId = 1;

        await SeedPlayersAsync(
            (Id: 42, GameId: gameId, Status: PlayerStatus.Active, CampaignOffsetId: 999));

        var result = await _handler.Handle(new GetPlayersRequest(gameId), CancellationToken.None);

        var player = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.Id, Is.EqualTo(42));
            Assert.That(player.GameId, Is.EqualTo(gameId));
            Assert.That(player.RulerTitle, Is.EqualTo("Title 42"));
            Assert.That(player.RulerName, Is.EqualTo("Ruler 42"));
            Assert.That(player.FactionName, Is.EqualTo("Faction 42"));
            Assert.That(player.FactionColour, Is.EqualTo("#00002a"), "The colour is what lets the client draw a faction the way its player chose.");
            Assert.That(player.RulerPortraitPath, Is.EqualTo("/portraits/03_east_asian_ruler.png"),
                "The projection is the only thing that puts a portrait on this path, so dropping it here would empty the game toolbar silently.");
            Assert.That(player.Status, Is.EqualTo(PlayerStatus.Active));
            Assert.That(player.CampaignOffsetId, Is.EqualTo(999));
        }
    }

    private async Task SeedPlayersAsync(params (int Id, int GameId, PlayerStatus Status, int CampaignOffsetId)[] players)
    {
        await using var context = new AscendantEmpireContext(DbOptions);

        int[] gameIds = [.. players.Select(p => p.GameId).Distinct()];

        foreach (int gameId in gameIds)
        {
            context.GameIndex.Add(new GameIndex
            {
                Id = gameId,
                GameName = $"Game {gameId}",
                NumberOfPlayers = players.Count(p => p.GameId == gameId),
                CreatedAt = DateTimeOffset.UtcNow,
                CreationStatus = GameCreationStatus.SetupComplete,
                PlayStatus = GamePlayStatus.InProgress,
                GenerationSeed = 0,
                BattleMapSize = 50,
            });
        }

        foreach ((int id, int gameId, PlayerStatus status, int campaignOffsetId) in players)
        {
            context.Players.Add(new AscendantEmpire.Domain.Entities.Games.Player
            {
                Id = id,
                GameId = gameId,
                Status = status,
                RulerName = $"Ruler {id}",
                RulerTitle = $"Title {id}",
                FactionName = $"Faction {id}",
                // Derived from the id so each player's colour is distinct, as the per-game unique index requires.
                FactionColour = $"#{id:x6}",
                PeopleName = "People",
                Demonym = "Person",
                CultureName = "Culture",
                Language = "Language",
                // Picked by the id for the same reason the colour is derived from it: a shared value could not tell
                // a projection that reads the right row from one that reads any row.
                RulerPortraitPath = RulerPortraitPaths.ForIndex(id),
                CampaignOffsetId = campaignOffsetId
            });
        }

        await context.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        EfServiceProvider.Dispose();
    }
}
