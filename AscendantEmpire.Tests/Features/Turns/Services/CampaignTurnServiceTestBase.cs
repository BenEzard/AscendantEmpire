using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Shared setup and seeding helpers for <see cref="CampaignTurnService"/> integration tests.
///
/// Each test uses a real per-test SQL Server database (via <see cref="GameSqlServerTestBase"/>).
/// The minimum game/player data is seeded through <see cref="GameSqlServerTestBase.SeedGameAsync"/>,
/// and turn-queue rows are inserted directly so each test controls exactly the state it needs.
/// </summary>
public abstract class CampaignTurnServiceTestBase : GameSqlServerTestBase
{
    internal CampaignTurnService Service = null!;

    [SetUp]
    public async Task SetUp()
    {
        // 2×2 map, 2 players — keeps per-test DB work fast.
        await SeedGameAsync(2, 2, 2);

        Service = new CampaignTurnService(
            new NullLogger<CampaignTurnService>(),
            DbFactory);
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
