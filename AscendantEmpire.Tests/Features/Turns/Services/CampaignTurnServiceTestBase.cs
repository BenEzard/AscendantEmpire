using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Shared setup and seeding helpers for <see cref="CampaignTurnService"/> tests.
/// </summary>
/// <remarks>
/// <para>
/// The service is read-only — every lookup is ordinary EF LINQ over the turn queue — so these tests sit on the in-memory tier rather than standing up a
/// SQL Server container.
/// </para>
/// <para>
/// Only a game, its players and the turn-queue rows are seeded. The service never looks at the campaign map, so no map or gallery data is created.
/// Turn-queue rows are inserted directly, letting each test arrange exactly the state it needs without going through the setup service.
/// </para>
/// </remarks>
public abstract class CampaignTurnServiceTestBase : InMemoryTestBase
{
    /// <summary>The number of players seeded into each test game, and therefore the number of participants in a seeded queue.</summary>
    internal int _numberOfPlayers = 2;

    internal CampaignTurnService Service = null!;

    [SetUp]
    public void SetUp()
    {
        Service = new CampaignTurnService(
            new NullLogger<CampaignTurnService>(),
            DbFactory);
    }

    /// <summary>
    /// Seeds a <see cref="GameIndex"/> with <paramref name="numberOfPlayers"/> active players.
    /// </summary>
    /// <returns>The Id of the seeded <see cref="GameIndex"/>.</returns>
    protected async Task<int> SeedGameWithPlayersAsync(int numberOfPlayers)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            GameName = "Turn Service Test Game",
            // No campaign map is seeded, so this points at nothing — the service never dereferences it.
            CampaignMapIndexId = 0,
            NumberOfPlayers = numberOfPlayers,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            HorizontalWrap = false,
            VerticalWrap = false,
            GenerationSeed = 0,
            BattleMapSize = 50,
        };
        ctx.GameIndex.Add(game);
        await ctx.SaveChangesAsync();

        for (int playerNumber = 1; playerNumber <= numberOfPlayers; playerNumber++)
        {
            ctx.Players.Add(new Player
            {
                GameId = game.Id,
                UserId = null,
                RulerTitle = "King",
                RulerName = $"Player {playerNumber}",
                FactionName = $"Faction {playerNumber}",
                Status = PlayerStatus.Active
            });
        }
        await ctx.SaveChangesAsync();

        return game.Id;
    }

    /// <summary>
    /// Inserts a Campaign <see cref="TurnQueue"/> for <paramref name="gameId"/> with two participants.
    /// The status of each participant is controlled by the corresponding parameter so individual
    /// test cases can arrange exactly the scenario they need without touching the service.
    /// </summary>
    /// <param name="gameId">The game to attach the queue to.</param>
    /// <param name="player1Status">Status assigned to the participant with TurnOrder 1.</param>
    /// <param name="player2Status">Status assigned to the participant with TurnOrder 2.</param>
    protected async Task SeedCampaignTurnQueueAsync(
        int gameId,
        TurnQueueParticipantStatus player1Status,
        TurnQueueParticipantStatus player2Status)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        // Resolve the two players that were created by SeedGameWithPlayersAsync.
        List<int> playerIds = [.. ctx.Players
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .Take(2)];

        var queue = new TurnQueue
        {
            GameId = gameId,
            QueueType = TurnQueueType.Campaign,
            BattleId = null,
            CurrentRoundNumber = 1,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        ctx.TurnQueues.Add(queue);
        await ctx.SaveChangesAsync();

        ctx.TurnQueueParticipants.AddRange(
            new TurnQueueParticipant
            {
                TurnQueueId = queue.Id,
                PlayerId = playerIds[0],
                TurnOrder = 1,
                Status = player1Status,
                IsActiveInLane = true
            },
            new TurnQueueParticipant
            {
                TurnQueueId = queue.Id,
                PlayerId = playerIds[1],
                TurnOrder = 2,
                Status = player2Status,
                IsActiveInLane = true
            });

        await ctx.SaveChangesAsync();
    }
}
