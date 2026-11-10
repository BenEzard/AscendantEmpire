using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Seeding shared by the Player Turn fixtures: a game, two players, and a campaign turn queue to hang turns off.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately hand-built rather than going through <c>GameSqlServerTestBase</c>. Nothing here needs a map, a catalogue or start positions - only a real
/// <see cref="TurnQueue"/> row for the foreign key and the filtered unique index to bite on - and the full game seed costs several seconds a test for
/// nothing.
/// </para>
/// <para>
/// Containerised because the service under test is raw SQL and a database-enforced index, neither of which the in-memory provider reproduces. See
/// <c>docs/testing.md</c>.
/// </para>
/// </remarks>
internal abstract class PlayerTurnTestBase : SqlServerTestBase
{
    protected const int _ROUND_NUMBER = 1;

    protected int GameId { get; private set; }
    protected int FirstPlayerId { get; private set; }
    protected int SecondPlayerId { get; private set; }
    protected int CampaignQueueId { get; private set; }

    /// <summary>The service under test, wired to the per-test database.</summary>
    protected IPlayerTurnService Service { get; private set; } = null!;

    [SetUp]
    public async Task SeedTurnQueueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = "Player Turn Tests",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = TestGameSettings.MINIMUM_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        GameId = game.Id;

        Player firstPlayer = BuildPlayer(GameId, "Faction 1", campaignOffsetId: 1);
        Player secondPlayer = BuildPlayer(GameId, "Faction 2", campaignOffsetId: 2);

        context.Players.AddRange(firstPlayer, secondPlayer);
        await context.SaveChangesAsync();

        FirstPlayerId = firstPlayer.Id;
        SecondPlayerId = secondPlayer.Id;

        CampaignQueueId = await AddQueueAsync(context, TurnQueueType.Campaign);

        Service = new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory);
    }

    /// <summary>
    /// Adds a turn lane to the seeded game and returns its id.
    /// </summary>
    /// <remarks>
    /// A battle lane needs no Battle row: <c>BattleId</c> is nullable, and none of the rules under test read it. What matters is that it is a second queue,
    /// so that the per-queue index and the per-turn counter can be shown to be scoped to a lane rather than to a game.
    /// </remarks>
    protected static async Task<int> AddQueueAsync(AscendantEmpireContext context, TurnQueueType queueType)
    {
        var queue = new TurnQueue
        {
            GameId = context.GameIndex.OrderBy(g => g.Id).Select(g => g.Id).First(),
            QueueType = queueType,
            BattleId = null,
            CurrentRoundNumber = _ROUND_NUMBER,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        context.TurnQueues.Add(queue);
        await context.SaveChangesAsync();

        return queue.Id;
    }

    /// <summary>
    /// Opens a turn through the service and returns its id, failing the test rather than returning garbage if the open did not succeed.
    /// </summary>
    protected async Task<int> OpenTurnAsync(int turnQueueId, int playerId, int roundNumber = _ROUND_NUMBER)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var result = await Service.OpenPlayerTurnAsync(context, turnQueueId, playerId, roundNumber);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    /// <summary>
    /// A slot carrying placeholders, with every value that has to be unique within a game derived from
    /// <paramref name="campaignOffsetId"/>.
    /// </summary>
    /// <remarks>
    /// The identity names are unique per game - see UX_Player_Game_PeopleName and its siblings - so a shared
    /// placeholder would have these fixtures failing on an insert rather than on the Turn behaviour they are about. The
    /// portrait stays on the default, the one value that rule exempts.
    /// </remarks>
    private static Player BuildPlayer(int gameId, string factionName, int campaignOffsetId)
    {
        return new Player
        {
            GameId = gameId,
            UserId = null,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = factionName,
            FactionName = factionName,
            FactionColour = null,
            PeopleName = $"People {campaignOffsetId}",
            Demonym = $"Person {campaignOffsetId}",
            Language = "Language",
            RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
            Status = PlayerStatus.Active,
            CampaignOffsetId = campaignOffsetId,
        };
    }
}
