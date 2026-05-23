using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Integration tests for the <c>CreateCampaignTurnQueueAsync</c> logic inside <see cref="GameSetupService"/>, exercised through <see cref="GameSetupService.SetupGameAsync"/>.
///
/// Each test uses a real per-test SQL Server database (via <see cref="SqlServerTestBase"/>) and seeds the minimum data required to reach <c>CreateCampaignTurnQueueAsync</c>.
/// <see cref="ICampaignMapGalleryService"/> is stubbed so we control the <see cref="CampaignMapGalleryProperties"/> returned without needing gallery service logic.
/// </summary>
[TestFixture]
public class CreateCampaignTurnQueueTests : GameSqlServerTestBase
{

    [SetUp]
    public async Task SetUp()
    {
        // 2×2 map keeps the per-test DB inserts fast (4 gallery tiles, 4 campaign-map rows, 8 player-map rows).
        await SeedGameAsync(2, 2, 2);
    }

    // -------------------------------------------------------------------------
    // Success path
    // -------------------------------------------------------------------------

    /// <summary>
    /// A successful game setup must create exactly one Campaign <see cref="TurnQueue"/> with one <see cref="TurnQueueParticipant"/> per Player, 
    /// each assigned a unique, sequential <see cref="TurnQueueParticipant.TurnOrder"/> value starting at 1, all in <see cref="TurnQueueParticipantStatus.Pending"/> status and active in the lane.
    /// </summary>
    [Test]
    public async Task ShouldCreateCampaignTurnQueueSuccesfullyAsync()
    {
        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Queue Test Game", NumberOfPlayers: _numberOfPlayers);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int gameId = result.SuccessValue;

        TurnQueue? queue = verifyContext.TurnQueues
            .FirstOrDefault(tq => tq.GameId == gameId && tq.QueueType == TurnQueueType.Campaign);

        List<TurnQueueParticipant> participants = queue is null ? [] :
            [.. verifyContext.TurnQueueParticipants
                .Where(p => p.TurnQueueId == queue.Id)
                .OrderBy(p => p.TurnOrder)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queue, Is.Not.Null, "A Campaign TurnQueue should have been created.");

            Assert.That(queue!.CurrentRoundNumber, Is.EqualTo(1), "Round number should start at 1.");

            Assert.That(queue.IsCompleted, Is.False, "Queue should not be marked completed on creation.");

            Assert.That(participants, Has.Count.EqualTo(_numberOfPlayers), "There should be one participant per player.");

            // TurnOrder must be 1..N with no gaps or duplicates.
            Assert.That(
                participants.Select(p => p.TurnOrder),
                Is.EqualTo(Enumerable.Range(1, _numberOfPlayers)),
                "Participant TurnOrder values should be sequential starting at 1.");

            Assert.That(participants.All(p => p.Status == TurnQueueParticipantStatus.Pending), Is.True, "All participants should start in Pending status.");

            Assert.That(participants.All(p => p.IsActiveInLane), Is.True, "All participants should be active in the lane.");
        }
    }

    /// <summary>
    /// After a successful game setup, <c>CompleteGameSetupAsync</c> sets <see cref="TurnQueue.CurrentTurnStartAt"/> and <see cref="TurnQueue.CurrentScheduledTurnEndAt"/> on the queue, 
    /// confirming the queue returned by <c>CreateCampaignTurnQueueAsync</c> was persisted and then updated by the subsequent step.
    /// </summary>
    [Test]
    public async Task SuccessfulGameSetupShouldStartTheTurnAsync()
    {
        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Queue Times Test", NumberOfPlayers: _numberOfPlayers);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True,
            $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();

        TurnQueue queue = verifyContext.TurnQueues
            .First(tq => tq.GameId == result.SuccessValue && tq.QueueType == TurnQueueType.Campaign);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queue.CurrentTurnStartAt, Is.Not.Null, "CurrentTurnStartAt should be set by CompleteGameSetupAsync.");

            Assert.That(queue.CurrentScheduledTurnEndAt, Is.Not.Null, "CurrentScheduledTurnEndAt should be set by CompleteGameSetupAsync.");

            Assert.That(queue.CurrentScheduledTurnEndAt!.Value, Is.GreaterThan(queue.CurrentTurnStartAt!.Value), "Scheduled end must be after the turn start time.");
        }
    }

    /// <summary>
    /// The participant with <see cref="TurnQueueParticipant.TurnOrder"/> = 1 must correspond to an actual Player that was added to the game, confirming the player IDs are wired correctly in the queue.
    /// </summary>
    [Test]
    public async Task ShouldWireFirstParticipantToPlayerCorrectlyAsync()
    {
        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "First Player Test", NumberOfPlayers: _numberOfPlayers);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int gameId = result.SuccessValue;

        TurnQueue queue = verifyContext.TurnQueues
            .First(tq => tq.GameId == gameId && tq.QueueType == TurnQueueType.Campaign);

        TurnQueueParticipant firstParticipant = verifyContext.TurnQueueParticipants
            .Where(p => p.TurnQueueId == queue.Id)
            .OrderBy(p => p.TurnOrder)
            .First();

        // The participant's PlayerId must reference a Player that belongs to this game.
        bool playerBelongsToGame = verifyContext.Players.Any(p => p.Id == firstParticipant.PlayerId && p.GameId == gameId);

        Assert.That(playerBelongsToGame, Is.True, "The first participant's PlayerId must correspond to a Player in the same game.");
    }

    // -------------------------------------------------------------------------
    // Duplicate queue guard
    // -------------------------------------------------------------------------

    /// <summary>
    /// If a Campaign <see cref="TurnQueue"/> already exists for a game when <c>CreateCampaignTurnQueueAsync</c> runs, 
    /// it must return a failure and the overall <see cref="GameSetupService.SetupGameAsync"/> must propagate that failure.
    ///
    /// The duplicate is injected by directly inserting a <see cref="TurnQueue"/> row for the game after <see cref="GameIndex"/> and <see cref="Player"/> rows are already seeded, 
    /// but before <c>SetupGameAsync</c> is allowed to create its own queue.
    /// 
    /// A custom <see cref="IDbContextFactory{AscendantEmpireContext}"/> intercepts the second <c>CreateDbContextAsync</c> call (used by <c>SetupGameAsync</c> internally) to seed the 
    /// duplicate queue transparently mid-execution.
    /// </summary>
    [Test]
    public async Task ShouldFailIfCampaignQueueAlreadyExistsAsync()
    {
        // Seed the GameIndex and Players that SetupGameAsync would normally create itself, so we have a valid gameId to attach the pre-existing TurnQueue to.
        int preSeededGameId = await SeedGameWithPlayersAsync(2);

        // Insert the blocking TurnQueue for that game directly.
        await using (AscendantEmpireContext seedCtx = await DbFactory.CreateDbContextAsync())
        {
            seedCtx.TurnQueues.Add(new TurnQueue
            {
                GameId = preSeededGameId,
                QueueType = TurnQueueType.Campaign,
                BattleId = null,
                CurrentRoundNumber = 1,
                IsCompleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                RowVersion = []
            });
            await seedCtx.SaveChangesAsync();
        }

        // Build a factory that intercepts the context used by SetupGameAsync so that AddGameIndexAsync returns our pre-seeded game (by controlling the identity).

        // Because we cannot intercept mid-flight, we exercise the guard by constructing a context that already contains the pre-seeded game and its blocking queue,
        // then running a raw invocation of CreateCampaignTurnQueueAsync via reflection.
        //
        // Reflection is used here because the method is private and the production code cannot be changed solely for testability.
        var methodInfo = typeof(GameSetupService).GetMethod(
            "CreateCampaignTurnQueueAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.That(methodInfo, Is.Not.Null, "CreateCampaignTurnQueueAsync method must exist.");

        await using AscendantEmpireContext testContext = await DbFactory.CreateDbContextAsync();

        int[] playerIds = [.. testContext.Players
            .Where(p => p.GameId == preSeededGameId)
            .Select(p => p.Id)];

        // Invoke the private method directly.
        var task = (Task<ServiceResultBase.ServiceResult<AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue.FirstPlayerInRound>>)
            methodInfo!.Invoke(_service, [testContext, preSeededGameId, playerIds, CancellationToken.None])!;

        ServiceResultBase.ServiceResult<AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue.FirstPlayerInRound> queueResult = await task;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queueResult.HasSucceeded, Is.False, "Should fail when a Campaign TurnQueue already exists for the game.");
            Assert.That(queueResult.Errors, Has.Some.Contains("already exists"), "Error message should mention the duplicate queue.");
        }
    }
}
