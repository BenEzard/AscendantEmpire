using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Game;

/// <summary>
/// Cover for the endpoint side of deleting a game: whether the job should be created at all, and what it carries when it is.
/// </summary>
/// <remarks>
/// Tier 2. The handler only reads, inserts one row and writes to a channel — the destructive part lives in <c>GameDeleteExecutor</c>, which needs a real
/// database and is covered separately.
/// </remarks>
[TestFixture]
public class InitiateGameDeleteHandlerTests : InMemoryTestBase
{
    private BackgroundProcessingChannel _channel = null!;

    [SetUp]
    public void SetUp()
    {
        _channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings { ChannelCapacity = 10 }));
    }

    [Test, Description("A game that exists is queued for deletion and its id travels on the job, since a Processing row has no foreign key to say what it is about.")]
    public async Task ShouldQueueTheJobCarryingTheGameIdWhenTheGameExistsAsync()
    {
        int gameId = await SeedGameAsync("Doomed");

        ServiceResult<int> result = await HandleAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        Processing job = await context.Processing.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
            Assert.That(result.SuccessValue, Is.EqualTo(job.ProcessingId), "The caller needs the processId back so it can poll the job.");
            Assert.That(job.ProcessType, Is.EqualTo(ProcessType.DeleteGame));
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Pending));
            Assert.That(ReadGameId(job.MetadataJson), Is.EqualTo(gameId));
        }
    }

    [Test, Description("The queued processId reaches the channel, because a job that is only persisted and never enqueued would sit Pending for ever.")]
    public async Task ShouldWriteTheProcessIdToTheChannelWhenTheJobIsQueuedAsync()
    {
        int gameId = await SeedGameAsync("Doomed");

        ServiceResult<int> result = await HandleAsync(gameId);

        bool wasRead = _channel.Reader.TryRead(out int queuedProcessId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wasRead, Is.True);
            Assert.That(queuedProcessId, Is.EqualTo(result.SuccessValue));
        }
    }

    [Test, Description("Deleting a game that isn't there answers 404 rather than queuing a job that could only fail once it reached the executor.")]
    public async Task ShouldReturnNotFoundWhenTheGameDoesNotExistAsync()
    {
        ServiceResult<int> result = await HandleAsync(gameId: 999);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(context.Processing, Is.Empty, "Nothing should have been queued.");
        }
    }

    [TestCase(ProcessingStatus.Pending, Description = "A queued delete of the same game blocks a second one, because two purges racing would delete rows out from under each other.")]
    [TestCase(ProcessingStatus.InProgress, Description = "A running delete of the same game blocks a second one, because two purges racing would delete rows out from under each other.")]
    public async Task ShouldReturnConflictWhenADeleteOfThatGameIsAlreadyUnderwayAsync(ProcessingStatus existingStatus)
    {
        int gameId = await SeedGameAsync("Doomed");
        await SeedDeleteJobAsync(gameId, existingStatus);

        ServiceResult<int> result = await HandleAsync(gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
    }

    [TestCase(ProcessingStatus.Complete, Description = "A finished delete does not block a later one: the game id is reused only if that first attempt left the game standing.")]
    [TestCase(ProcessingStatus.Failed, Description = "A failed delete must be retryable, or a purge that broke half way through could never be finished off.")]
    public async Task ShouldQueueTheJobWhenTheOnlyOtherDeleteOfThatGameHasFinishedAsync(ProcessingStatus existingStatus)
    {
        int gameId = await SeedGameAsync("Doomed");
        await SeedDeleteJobAsync(gameId, existingStatus);

        ServiceResult<int> result = await HandleAsync(gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
    }

    [Test, Description("A delete already running against a different game does not block this one, since the two purges touch no rows in common.")]
    public async Task ShouldQueueTheJobWhenAnotherGameIsBeingDeletedAsync()
    {
        int gameId = await SeedGameAsync("Doomed");
        int otherGameId = await SeedGameAsync("Innocent bystander");

        await SeedDeleteJobAsync(otherGameId, ProcessingStatus.InProgress);

        ServiceResult<int> result = await HandleAsync(gameId);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
    }

    [Test, Description("A game id of zero or less is rejected by the validator, so a malformed route never reaches the database.")]
    public void ShouldFailValidationWhenTheGameIdIsNotPositive()
    {
        var validator = new InitiateGameDeleteRequestValidator();

        Assert.That(validator.Validate(new InitiateGameDeleteRequest(0)).IsValid, Is.False);
    }

    // ---------------------------------------------------------------------

    private async Task<ServiceResult<int>> HandleAsync(int gameId)
    {
        // A fresh context per call, mirroring the scoped one the handler is resolved with in the API.
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var handler = new InitiateGameDeleteHandler(new NullLogger<InitiateGameDeleteHandler>(), context, _channel);

        return await handler.Handle(new InitiateGameDeleteRequest(gameId), CancellationToken.None);
    }

    private async Task<int> SeedGameAsync(string gameName)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.AwaitingPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30,
            MapWidth = 2,
            MapHeight = 2
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        return game.Id;
    }

    private async Task SeedDeleteJobAsync(int gameId, ProcessingStatus status)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Processing.Add(new Processing
        {
            ProcessType = ProcessType.DeleteGame,
            Status = status,
            StartedAt = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new Dictionary<string, int> { ["gameId"] = gameId })
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Reads the game id back out of a job's metadata the way the executor will, rather than trusting the writer to have got it right.</summary>
    private static int? ReadGameId(string? metadataJson)
    {
        if (metadataJson is null)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(metadataJson);

        return document.RootElement.TryGetProperty("gameId", out JsonElement gameId) ? gameId.GetInt32() : null;
    }
}
