using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetGames;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Game;

/// <summary>
/// Cover for the System panel's games listing.
/// </summary>
/// <remarks>
/// Tier 2: the handler is a plain LINQ projection with no constraint enforcement, bulk operation, raw SQL or transaction behind it, so an in-memory store
/// is enough.
/// </remarks>
[TestFixture]
public class GetAllGamesHandlerTests : InMemoryTestBase
{
    private GetAllGamesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new GetAllGamesHandler(new NullLogger<GetAllGamesHandler>(), DbFactory);
    }

    [Test, Description("Every game is listed whatever state it is in, because a game that failed setup or was abandoned is exactly the kind someone came here to delete.")]
    public async Task ShouldListGamesInEveryStatusAsync()
    {
        await SeedGameAsync("Failed setup", GameCreationStatus.SetupFailed, GamePlayStatus.SetupInProgress);
        await SeedGameAsync("Waiting", GameCreationStatus.SetupComplete, GamePlayStatus.AwaitingPlayers);
        await SeedGameAsync("Playing", GameCreationStatus.SetupComplete, GamePlayStatus.InProgress);
        await SeedGameAsync("Given up on", GameCreationStatus.SetupComplete, GamePlayStatus.Abandoned);

        ServiceResult<IReadOnlyCollection<GameSummary>> result = await _handler.Handle(new GetAllGamesRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        Assert.That(result.SuccessValue!.Select(g => g.GameName),
            Is.EquivalentTo(new[] { "Failed setup", "Waiting", "Playing", "Given up on" }));
    }

    [Test, Description("The claimed count counts only slots an active player actually occupies, so a half-full lobby does not read as full.")]
    public async Task ShouldCountOnlyClaimedSlotsWhenSomeSlotsAreStillFreeAsync()
    {
        int gameId = await SeedGameAsync("Half full", GameCreationStatus.SetupComplete, GamePlayStatus.AwaitingPlayers, numberOfPlayers: 4);

        // One user in the game, plus a free slot, plus a slot whose player has been eliminated - only the first counts.
        int userId = await SeedUserAsync("claimant");
        await SeedPlayerAsync(gameId, userId, PlayerStatus.Active);
        await SeedPlayerAsync(gameId, userId: null, PlayerStatus.Active);
        await SeedPlayerAsync(gameId, await SeedUserAsync("loser"), PlayerStatus.Eliminated);

        ServiceResult<IReadOnlyCollection<GameSummary>> result = await _handler.Handle(new GetAllGamesRequest(), CancellationToken.None);

        GameSummary game = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.ClaimedPlayerCount, Is.EqualTo(1));
            Assert.That(game.NumberOfPlayers, Is.EqualTo(4), "The slot count is what the game was set up with, not how many rows exist.");
        }
    }

    [Test, Description("The newest game is listed first, because a game someone has just broken is the one they are most likely to be here to remove.")]
    public async Task ShouldListTheNewestGameFirstAsync()
    {
        await SeedGameAsync("Oldest", GameCreationStatus.SetupComplete, GamePlayStatus.Completed, createdAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedGameAsync("Newest", GameCreationStatus.SetupComplete, GamePlayStatus.InProgress, createdAt: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedGameAsync("Middle", GameCreationStatus.SetupComplete, GamePlayStatus.InProgress, createdAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        ServiceResult<IReadOnlyCollection<GameSummary>> result = await _handler.Handle(new GetAllGamesRequest(), CancellationToken.None);

        Assert.That(result.SuccessValue!.Select(g => g.GameName), Is.EqualTo(new[] { "Newest", "Middle", "Oldest" }).AsCollection);
    }

    [Test, Description("An empty database answers with an empty list rather than a failure, so the page renders its empty state instead of an error.")]
    public async Task ShouldReturnAnEmptyListWhenThereAreNoGamesAsync()
    {
        ServiceResult<IReadOnlyCollection<GameSummary>> result = await _handler.Handle(new GetAllGamesRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue, Is.Empty);
        }
    }

    // ---------------------------------------------------------------------

    private async Task<int> SeedGameAsync(
        string gameName,
        GameCreationStatus creationStatus,
        GamePlayStatus playStatus,
        int numberOfPlayers = 2,
        DateTimeOffset? createdAt = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = numberOfPlayers,
            CreationStatus = creationStatus,
            PlayStatus = playStatus,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30,
            MapWidth = 2,
            MapHeight = 2
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        return game.Id;
    }

    private async Task<int> SeedUserAsync(string username)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var user = new User
        {
            Username = username,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            InactiveReasonType = InactiveReasonType.Active
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task SeedPlayerAsync(int gameId, int? userId, PlayerStatus status)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(new Player
        {
            GameId = gameId,
            UserId = userId,
            RulerTitle = "King",
            RulerName = "Test",
            FactionName = $"Faction {Guid.NewGuid():N}",
            Status = status
        });

        await context.SaveChangesAsync();
    }
}
