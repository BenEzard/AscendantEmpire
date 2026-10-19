using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Game.Executors;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.Tests.Features.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Game;

/// <summary>
/// Cover for the purge itself: that deleting one game removes everything scoped to it, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Tier 3, on all four counts <c>testing.md</c> names: the ordering the purge depends on is only real when the database is enforcing the foreign keys, every
/// step is an <c>ExecuteDeleteAsync</c>, the whole thing runs in an explicit transaction, and the identity reseed is raw <c>DBCC</c>.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c> rather than by hand-seeded rows, because a hand-seeded game would only contain the tables someone
/// remembered to seed — and the failure this fixture guards against is precisely a table nobody remembered.
/// </para>
/// </remarks>
[TestFixture]
public class GameDeleteExecutorTests : GameSqlServerTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await SeedGameAsync(2, 2, 2);
    }

    [Test, Description("Every row scoped to the game goes, across all of the tables game setup populates, not just the ones it is obvious to remember.")]
    public async Task ShouldRemoveEveryRowScopedToTheGameWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.GameIndex.AnyAsync(g => g.Id == gameId), Is.False, "The game itself should be gone.");
            Assert.That(await context.Players.AnyAsync(p => p.GameId == gameId), Is.False);
            Assert.That(await context.CampaignMaps.AnyAsync(t => t.GameId == gameId), Is.False);
            Assert.That(await context.PlayerCampaignMaps.AnyAsync(t => t.GameId == gameId), Is.False);
            Assert.That(await context.UnitInstances.AnyAsync(u => u.GameId == gameId), Is.False);
            Assert.That(await context.TurnQueues.AnyAsync(q => q.GameId == gameId), Is.False);
            Assert.That(await context.TurnQueueParticipants.AnyAsync(), Is.False, "The only turn queue in the database belonged to the deleted game.");
            Assert.That(await context.PlayerUnlockedTemplates.AnyAsync(), Is.False, "Unlock rows are player-scoped, even when the template they name survives.");
            Assert.That(await context.PlayerUnlockedComponents.AnyAsync(), Is.False);
            Assert.That(await context.PlayerUnlockedAssets.AnyAsync(), Is.False, "Asset unlocks are player-scoped, even though the Asset they name survives.");
            Assert.That(await context.PlayerUnlockedAssetMetaCategories.AnyAsync(), Is.False);
            Assert.That(await context.PlayerOfficerRanks.AnyAsync(), Is.False);
        }
    }

    [Test, Description("Movement history is purged with its game, and first, because its rows name the tiles, players, templates and turns later steps delete.")]
    public async Task ShouldRemoveTheGamesMovementHistoryWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedMovementHistoryAsync(gameId);

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.MovementHistory.AnyAsync(h => h.GameId == gameId), Is.False);
        }
    }

    [Test, Description("Sighting reports, their paths and known Forces go with their game, and before the turns, tiles, templates and players they name.")]
    public async Task ShouldRemoveTheGamesSightingReportsAndKnownForcesWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedSightingsAsync(gameId);

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == gameId), Is.False);
            Assert.That(await context.PlayerReportPathTiles.AnyAsync(), Is.False, "The only reports in the database belonged to the deleted game.");
            Assert.That(await context.PlayerKnownForces.AnyAsync(k => k.GameId == gameId), Is.False);
        }
    }

    [Test, Description("Structure history, structure reports and known Structures go with their game, and before the Structures, turns, tiles and players they name.")]
    public async Task ShouldRemoveTheGamesStructureHistoryAndStructureSightingsWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedStructureSightingsAsync(gameId);

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.StructureHistory.AnyAsync(h => h.GameId == gameId), Is.False);
            Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == gameId), Is.False);
            Assert.That(await context.PlayerKnownStructures.AnyAsync(k => k.GameId == gameId), Is.False);
            Assert.That(await context.StructureInstances.AnyAsync(s => s.GameId == gameId), Is.False);
        }
    }

    [Test, Description("Battle history goes with its game, and before the battles, turns, tiles and players it names - the step whose absence failed real deletes on the PlayerTurn foreign key.")]
    public async Task ShouldRemoveTheGamesBattleHistoryWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedBattleHistoryAsync(gameId);

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.BattleHistory.AnyAsync(h => h.GameId == gameId), Is.False);
            Assert.That(await context.Battles.AnyAsync(b => b.GameId == gameId), Is.False);
            Assert.That(await context.BattleParticipants.AnyAsync(), Is.False, "The only battle in the database belonged to the deleted game.");
        }
    }

    [Test, Description("Players' views of a battle map go with their game, and before the turns, battle map tiles and players they name.")]
    public async Task ShouldRemoveTheGamesPlayerBattleMapViewsWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedPlayerBattleMapAsync(gameId);

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.PlayerBattleMaps.AnyAsync(v => v.GameId == gameId), Is.False);
            Assert.That(await context.BattleMap.AnyAsync(), Is.False, "The only battle map in the database belonged to the deleted game.");
            Assert.That(await context.BattleMapIndex.AnyAsync(), Is.False);
        }
    }

    [Test, Description("Players' sustenance modifiers go with their game, and before the players they name, while the biome catalogue they also name survives.")]
    public async Task ShouldRemoveTheGamesPlayerSustenanceModifiersWhenItIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        await SeedPlayerSustenanceModifiersAsync(gameId);

        int biomesBefore = await CountAsync(c => c.CampaignBiomes.CountAsync());

        Processing job = await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.PlayerSustenanceModifiers.AnyAsync(m => m.GameId == gameId), Is.False);
            Assert.That(await context.CampaignBiomes.CountAsync(), Is.EqualTo(biomesBefore), "Biomes are catalogue data, not the game's.");
        }
    }

    [Test, Description("Campaign map depletion goes with its game, and before the campaign map tiles it names, while another game's depletion survives.")]
    public async Task ShouldRemoveTheGamesCampaignMapDepletionWhenItIsDeletedAsync()
    {
        int doomedGameId = await SetupGameAsync("Doomed");
        int survivorGameId = await SetupGameAsync("Survivor");

        await SeedCampaignMapDepletionAsync(doomedGameId);
        await SeedCampaignMapDepletionAsync(survivorGameId);

        Processing job = await ExecuteDeleteAsync(doomedGameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.CampaignMapDepletions.AnyAsync(d => d.GameId == doomedGameId), Is.False);
            Assert.That(await context.CampaignMapDepletions.AnyAsync(d => d.GameId == survivorGameId), Is.True, "Only the deleted game's depletion goes.");
        }
    }

    [Test, Description("A game's MapResources, their amounts and layers, and every Player's knowledge, modifiers and switches for them go with it; the MapResource catalogue survives.")]
    public async Task ShouldRemoveTheGamesMapResourcesWhenItIsDeletedAsync()
    {
        int doomedGameId = await SetupGameAsync("Doomed");
        int survivorGameId = await SetupGameAsync("Survivor");

        MapResourceTestCatalogue catalogue;

        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            catalogue = await MapResourceTestSeed.SeedCatalogueAsync(seed);
        }

        await SeedMapResourcesAsync(doomedGameId, catalogue);
        await SeedMapResourcesAsync(survivorGameId, catalogue);

        int typesBefore = await CountAsync(c => c.MapResourceTypes.CountAsync());

        Processing job = await ExecuteDeleteAsync(doomedGameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(await context.MapResourceInstances.AnyAsync(i => i.GameId == doomedGameId), Is.False);
            Assert.That(await context.MapResourceInstanceAmounts.AnyAsync(a => a.GameId == doomedGameId), Is.False);
            Assert.That(await context.MapResourceInstanceLayers.AnyAsync(l => l.GameId == doomedGameId), Is.False);
            Assert.That(await context.PlayerKnownMapResources.AnyAsync(k => k.GameId == doomedGameId), Is.False);
            Assert.That(await context.PlayerMapResourceTypeKnowledge.AnyAsync(k => k.GameId == doomedGameId), Is.False);
            Assert.That(await context.PlayerMapResourceModifiers.AnyAsync(m => m.GameId == doomedGameId), Is.False);
            Assert.That(await context.PlayerMapResourceSwitches.AnyAsync(s => s.GameId == doomedGameId), Is.False);
            Assert.That(await context.MapResourceInstances.AnyAsync(i => i.GameId == survivorGameId), Is.True, "Only the deleted game's MapResources go.");
            Assert.That(await context.MapResourceTypes.CountAsync(), Is.EqualTo(typesBefore), "MapResourceTypes are catalogue data, not the game's.");
        }
    }

    [Test, Description("The job is marked complete rather than being left in progress, because the page polls that status to know when to refresh the list.")]
    public async Task ShouldMarkTheJobCompleteWhenThePurgeSucceedsAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        Processing job = await ExecuteDeleteAsync(gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Complete), job.ErrorMessage);
            Assert.That(job.ProgressPercent, Is.EqualTo(100));
            Assert.That(job.CompletedAt, Is.Not.Null);
        }
    }

    [Test, Description("A second game is left completely intact, because a purge that reached past its own game would take live games down with it.")]
    public async Task ShouldLeaveOtherGamesUntouchedWhenOneIsDeletedAsync()
    {
        int doomedGameId = await SetupGameAsync("Doomed");
        int survivingGameId = await SetupGameAsync("Survivor");

        int tilesBefore = await CountAsync(c => c.CampaignMaps.CountAsync(t => t.GameId == survivingGameId));
        int unitsBefore = await CountAsync(c => c.UnitInstances.CountAsync(u => u.GameId == survivingGameId));
        int playersBefore = await CountAsync(c => c.Players.CountAsync(p => p.GameId == survivingGameId));

        await ExecuteDeleteAsync(doomedGameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.GameIndex.AnyAsync(g => g.Id == survivingGameId), Is.True);
            Assert.That(await context.CampaignMaps.CountAsync(t => t.GameId == survivingGameId), Is.EqualTo(tilesBefore));
            Assert.That(await context.UnitInstances.CountAsync(u => u.GameId == survivingGameId), Is.EqualTo(unitsBefore));
            Assert.That(await context.Players.CountAsync(p => p.GameId == survivingGameId), Is.EqualTo(playersBefore));
        }
    }

    [Test, Description("Catalogue and reference data survives, since it is shared by every game and re-authoring it would be the real cost of a delete.")]
    public async Task ShouldKeepTheSharedCatalogueWhenAGameIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        // Setup leaves every slot unclaimed, so there is no account in the database unless one is put there.
        await SeedUserAsync();

        int componentsBefore = await CountAsync(c => c.UnitComponents.CountAsync());
        int tagsBefore = await CountAsync(c => c.Tags.CountAsync());
        int biomesBefore = await CountAsync(c => c.CampaignBiomes.CountAsync());
        int galleryTilesBefore = await CountAsync(c => c.CampaignMapGallery.CountAsync());
        int startPositionsBefore = await CountAsync(c => c.CampaignMapStartPositions.CountAsync());
        int assetsBefore = await CountAsync(c => c.Assets.CountAsync());
        int assetMetaCategoriesBefore = await CountAsync(c => c.AssetMetaCategories.CountAsync());

        await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitComponents.CountAsync(), Is.EqualTo(componentsBefore));
            Assert.That(await context.Tags.CountAsync(), Is.EqualTo(tagsBefore));
            Assert.That(await context.CampaignBiomes.CountAsync(), Is.EqualTo(biomesBefore));
            Assert.That(await context.CampaignMapGallery.CountAsync(), Is.EqualTo(galleryTilesBefore));
            Assert.That(await context.CampaignMapStartPositions.CountAsync(), Is.EqualTo(startPositionsBefore),
                "Start positions belong to the gallery map, not to any one game played on it.");
            Assert.That(await context.Assets.CountAsync(), Is.EqualTo(assetsBefore),
                "The asset catalogue is shared; only the per-player unlock rows pointing at it go.");
            Assert.That(await context.AssetMetaCategories.CountAsync(), Is.EqualTo(assetMetaCategoriesBefore));
            Assert.That(await context.Users.AnyAsync(), Is.True, "Accounts outlive the games they played.");
        }
    }

    [Test, Description("Global unit templates survive while the deleted game's own custom ones go, and another game's custom ones are left alone — the distinction the old wipe-everything script got wrong.")]
    public async Task ShouldDeleteOnlyTheDeletedGamesCustomTemplatesAsync()
    {
        int doomedGameId = await SetupGameAsync("Doomed");
        int survivingGameId = await SetupGameAsync("Survivor");

        int doomedTemplateId = await SeedCustomTemplateAsync(doomedGameId, "Doomed Levy");
        int survivingTemplateId = await SeedCustomTemplateAsync(survivingGameId, "Surviving Levy");

        int globalTemplatesBefore = await CountAsync(c => c.UnitTemplates.CountAsync(t => t.GameId == null && t.PlayerId == null));

        await ExecuteDeleteAsync(doomedGameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitTemplates.AnyAsync(t => t.Id == doomedTemplateId), Is.False);
            Assert.That(await context.UnitTemplateComponents.AnyAsync(c => c.UnitTemplateId == doomedTemplateId), Is.False);
            Assert.That(await context.UnitTemplates.AnyAsync(t => t.Id == survivingTemplateId), Is.True,
                "Another game's custom template has nothing to do with this purge.");
            Assert.That(await context.UnitTemplates.CountAsync(t => t.GameId == null && t.PlayerId == null), Is.EqualTo(globalTemplatesBefore),
                "A global template has no GameId and no PlayerId, so no game owns it.");
        }
    }

    [Test, Description("A session scoped to the deleted game is revoked and has its game context cleared, because nothing else would ever clear columns that carry no foreign key.")]
    public async Task ShouldRevokeAndClearSessionsScopedToTheGameAsync()
    {
        int gameId = await SetupGameAsync("Doomed");

        int playerId = await FirstPlayerIdAsync(gameId);
        int sessionId = await SeedSessionAsync(gameId, playerId);

        await ExecuteDeleteAsync(gameId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        RefreshSession session = await context.RefreshSessions.SingleAsync(s => s.Id == sessionId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session.RevokedAt, Is.Not.Null, "Anyone still in the game should be turned out of it.");
            Assert.That(session.GameId, Is.Null);
            Assert.That(session.PlayerId, Is.Null);
        }
    }

    [Test, Description("Deleting the last game resets the id counters, so a fresh start numbers from 1 instead of carrying on from whatever the abandoned games reached.")]
    public async Task ShouldResetIdentityCountersWhenTheLastGameIsDeletedAsync()
    {
        int gameId = await SetupGameAsync("The only game");

        await ExecuteDeleteAsync(gameId);

        int nextGameId = await InsertBareGameAsync("Next");

        Assert.That(nextGameId, Is.EqualTo(1));
    }

    [Test, Description("A table nothing has ever been inserted into keeps its untouched counter, because SQL Server gives a reseed value straight to the first row and would start it at id 0.")]
    public async Task ShouldLeaveTheCounterOfATableThatHasNeverBeenInsertedIntoAloneAsync()
    {
        int gameId = await SetupGameAsync("The only game");

        // TRUNCATE puts the identity back into the never-used state a table has when a migration has just created it, which is how the history tables met their first game.
        await ExecuteSqlAsync("TRUNCATE TABLE [units].[MovementHistory];");

        await ExecuteDeleteAsync(gameId);

        long? lastValue = await IdentityLastValueAsync("[units].[MovementHistory]");

        Assert.That(lastValue, Is.Null, "A reseed here would leave the counter set, and the next row inserted would be id 0.");
    }

    [Test, Description("Deleting one of several games leaves the counters alone, because reseeding while rows survive would hand out ids that collide with them.")]
    public async Task ShouldNotResetIdentityCountersWhileAnotherGameRemainsAsync()
    {
        int doomedGameId = await SetupGameAsync("Doomed");
        int survivingGameId = await SetupGameAsync("Survivor");

        await ExecuteDeleteAsync(doomedGameId);

        int nextGameId = await InsertBareGameAsync("Next");

        Assert.That(nextGameId, Is.GreaterThan(survivingGameId));
    }

    [Test, Description("A job whose metadata names no game fails loudly rather than quietly doing nothing, because a purge that silently no-ops looks identical to one that worked.")]
    public async Task ShouldMarkTheJobFailedWhenTheMetadataNamesNoGameAsync()
    {
        int gameId = await SetupGameAsync("Untouched");

        Processing job = await ExecuteDeleteAsync(metadataJson: "{}");

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(job.Status, Is.EqualTo(ProcessingStatus.Failed));
            Assert.That(job.ErrorMessage, Is.Not.Null);
            Assert.That(await context.GameIndex.AnyAsync(g => g.Id == gameId), Is.True, "Nothing should have been deleted.");
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>Runs the real game setup and returns the new Game's Id.</summary>
    private async Task<int> SetupGameAsync(string gameName)
    {
        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: gameName, NumberOfPlayers: _numberOfPlayers, BattleMapSize: 50, GenerationSeed: null);

        ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True,
            $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        return result.SuccessValue;
    }

    /// <summary>Runs the executor against a delete job for one game, and returns the job as it was left.</summary>
    private Task<Processing> ExecuteDeleteAsync(int gameId)
    {
        return ExecuteDeleteAsync(JsonSerializer.Serialize(new Dictionary<string, int> { ["gameId"] = gameId }));
    }

    /// <summary>
    /// Runs the executor against a delete job carrying the given metadata.
    /// </summary>
    /// <remarks>
    /// The job is tracked by its own context, exactly as the dispatcher hands it over, so the executor's progress writes go somewhere separate from the
    /// transaction its purge runs in.
    /// </remarks>
    private async Task<Processing> ExecuteDeleteAsync(string metadataJson)
    {
        await using AscendantEmpireContext jobContext = await DbFactory.CreateDbContextAsync();

        var job = new Processing
        {
            ProcessType = ProcessType.DeleteGame,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = metadataJson
        };

        jobContext.Processing.Add(job);
        await jobContext.SaveChangesAsync();

        var executor = new GameDeleteExecutor(
            jobContext,
            DbFactory,
            new RefreshSessionService(new NullLogger<RefreshSessionService>(), DbFactory),
            new NullLogger<GameDeleteExecutor>());

        await executor.ExecuteAsync(job, CancellationToken.None);

        return job;
    }

    /// <summary>Runs a count against a fresh context, so a "before" figure is read from the database rather than from a stale one.</summary>
    private async Task<int> CountAsync(Func<AscendantEmpireContext, Task<int>> count)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await count(context);
    }

    private async Task<int> FirstPlayerIdAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Players.Where(p => p.GameId == gameId).Select(p => p.Id).OrderBy(id => id).FirstAsync();
    }

    /// <summary>
    /// Opens a Turn in the game's campaign lane and records a Created and a Moved row against it for one of the starting Units.
    /// </summary>
    /// <remarks>
    /// Written straight to the table rather than through the lifecycle writer: setup places its starting armies with no history, and this fixture is about the purge reaching the rows rather than
    /// how they got there. The Moved row matters as well as the Created one, because it is the only kind that names a second tile.
    /// </remarks>
    private async Task SeedMovementHistoryAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TurnQueue campaignQueue = await context.TurnQueues.SingleAsync(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null);
        UnitInstance unit = await context.UnitInstances.Where(u => u.GameId == gameId).OrderBy(u => u.Id).FirstAsync();

        int otherTileId = await context.CampaignMaps
            .Where(t => t.GameId == gameId && t.CampaignMapId != unit.CampaignMapTileId)
            .Select(t => t.CampaignMapId)
            .FirstAsync();

        var turn = new PlayerTurn
        {
            TurnQueueId = campaignQueue.Id,
            PlayerId = unit.PlayerId,
            RoundNumber = campaignQueue.CurrentRoundNumber,
            StartedAt = DateTimeOffset.UtcNow
        };

        context.PlayerTurns.Add(turn);
        await context.SaveChangesAsync();

        context.MovementHistory.AddRange(
            new MovementHistory
            {
                GameId = gameId,
                UnitInstanceId = unit.Id,
                PlayerId = unit.PlayerId,
                UnitTemplateId = unit.UnitTemplateId,
                EventType = ForceHistoryEventType.Created,
                CampaignMapTileId = unit.CampaignMapTileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 1
            },
            new MovementHistory
            {
                GameId = gameId,
                UnitInstanceId = unit.Id,
                PlayerId = unit.PlayerId,
                UnitTemplateId = unit.UnitTemplateId,
                EventType = ForceHistoryEventType.Moved,
                FromCampaignMapTileId = unit.CampaignMapTileId,
                CampaignMapTileId = otherTileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 2
            });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a Turn and records one sighting of a starting Unit by the other player: a report with a path tile, and a known Force.
    /// </summary>
    /// <remarks>
    /// Written straight to the tables rather than through the sightings step, for the same reason as <see cref="SeedMovementHistoryAsync"/>: this fixture is about the purge reaching the rows.
    /// </remarks>
    private async Task SeedSightingsAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TurnQueue campaignQueue = await context.TurnQueues.SingleAsync(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null);
        UnitInstance unit = await context.UnitInstances.Where(u => u.GameId == gameId).OrderBy(u => u.Id).FirstAsync();
        int watcherId = await context.Players.Where(p => p.GameId == gameId && p.Id != unit.PlayerId).Select(p => p.Id).FirstAsync();

        var turn = new PlayerTurn
        {
            TurnQueueId = campaignQueue.Id,
            PlayerId = unit.PlayerId,
            RoundNumber = campaignQueue.CurrentRoundNumber,
            StartedAt = DateTimeOffset.UtcNow
        };

        context.PlayerTurns.Add(turn);
        await context.SaveChangesAsync();

        context.PlayerReports.Add(new PlayerReport
        {
            GameId = gameId,
            RecipientPlayerId = watcherId,
            RaisedDuringPlayerTurnId = turn.Id,
            ReportType = PlayerReportType.ForceSighted,
            SubjectUnitInstanceId = unit.Id,
            SubjectOwnerPlayerId = unit.PlayerId,
            SubjectUnitTemplateId = unit.UnitTemplateId,
            Fidelity = SightingFidelity.Detail,
            FirstCampaignMapTileId = unit.CampaignMapTileId,
            LastCampaignMapTileId = unit.CampaignMapTileId,
            FirstTurnSequence = 1,
            LastTurnSequence = 1,
            StillVisible = true,
            FirstWitnessId = 1,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = watcherId,
            PathTiles = [new PlayerReportPathTile { Ordinal = 0, CampaignMapTileId = unit.CampaignMapTileId }]
        });

        context.PlayerKnownForces.Add(new PlayerKnownForce
        {
            GameId = gameId,
            PlayerId = watcherId,
            UnitInstanceId = unit.Id,
            OwnerPlayerId = unit.PlayerId,
            UnitTemplateId = unit.UnitTemplateId,
            Status = KnownForceStatus.Sighted,
            Fidelity = SightingFidelity.Detail,
            LastSeenCampaignMapTileId = unit.CampaignMapTileId,
            LastSeenPlayerTurnId = turn.Id,
            LastSeenTurnSequence = 1,
            StillVisible = true
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a Turn and records one Structure's life in it - founded, then captured - with a structure report and a known Structure for the player it was taken from.
    /// </summary>
    /// <remarks>
    /// Written straight to the tables, for the same reason as <see cref="SeedSightingsAsync"/>. The watchtower catalogue row is not game-scoped and survives the purge, as catalogue data does.
    /// </remarks>
    private async Task SeedStructureSightingsAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TurnQueue campaignQueue = await context.TurnQueues.SingleAsync(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null);
        int[] playerIds = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
        int captorId = playerIds[0];
        int formerOwnerId = playerIds[1];
        int tileId = await context.CampaignMaps.Where(t => t.GameId == gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();

        var turn = new PlayerTurn
        {
            TurnQueueId = campaignQueue.Id,
            PlayerId = captorId,
            RoundNumber = campaignQueue.CurrentRoundNumber,
            StartedAt = DateTimeOffset.UtcNow
        };

        var watchtower = new Domain.Entities.Structures.Structure
        {
            Code = "watchtower",
            Name = "Watchtower",
            RequiresSlotType = Domain.Enums.Structures.TileSlotType.Primary,
            AllowedPositions = Domain.Enums.Structures.PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = 0,
            SightRange = 2,
            SightDirection = DetectionDirection.FullRadius,
            MapImagePath = "watchtower.png",
            FullImagePath = "watchtower-full.png"
        };

        context.PlayerTurns.Add(turn);
        context.Set<Domain.Entities.Structures.Structure>().Add(watchtower);
        await context.SaveChangesAsync();

        var structure = new StructureInstance
        {
            GameId = gameId,
            CampaignMapTileId = tileId,
            StructureId = watchtower.Id,
            SlotType = Domain.Enums.Structures.TileSlotType.Primary,
            Position = Domain.Enums.Structures.PositionOnTile.WholeTile,
            OwnedByPlayerId = captorId,
            CreatedOnTurnId = turn.Id
        };

        context.StructureInstances.Add(structure);
        await context.SaveChangesAsync();

        context.StructureHistory.AddRange(
            new Domain.Entities.Structures.StructureHistory
            {
                GameId = gameId,
                StructureInstanceId = structure.Id,
                StructureId = watchtower.Id,
                PlayerId = formerOwnerId,
                EventType = Domain.Enums.Structures.StructureHistoryEventType.Founded,
                CompletionPercent = 100,
                CampaignMapTileId = tileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 1
            },
            new Domain.Entities.Structures.StructureHistory
            {
                GameId = gameId,
                StructureInstanceId = structure.Id,
                StructureId = watchtower.Id,
                PlayerId = captorId,
                PreviousOwnerPlayerId = formerOwnerId,
                EventType = Domain.Enums.Structures.StructureHistoryEventType.Captured,
                CompletionPercent = 100,
                CampaignMapTileId = tileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 2
            });

        context.PlayerReports.Add(new PlayerReport
        {
            GameId = gameId,
            RecipientPlayerId = formerOwnerId,
            RaisedDuringPlayerTurnId = turn.Id,
            ReportType = PlayerReportType.StructureSighted,
            SubjectStructureInstanceId = structure.Id,
            SubjectStructureId = watchtower.Id,
            SubjectOwnerPlayerId = captorId,
            SubjectPreviousOwnerPlayerId = formerOwnerId,
            SubjectCompletionPercent = 100,
            Fidelity = SightingFidelity.Detail,
            FirstCampaignMapTileId = tileId,
            LastCampaignMapTileId = tileId,
            FirstTurnSequence = 1,
            LastTurnSequence = 2,
            StillVisible = true,
            AppearedInView = true,
            DestroyedInView = false,
            CompletedInView = true,
            CapturedInView = true,
            FirstWitnessId = 1,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = formerOwnerId
        });

        context.PlayerKnownStructures.Add(new PlayerKnownStructure
        {
            GameId = gameId,
            PlayerId = formerOwnerId,
            StructureInstanceId = structure.Id,
            StructureId = watchtower.Id,
            OwnerPlayerId = captorId,
            ObservedCompletionPercent = 100,
            Status = KnownStructureStatus.Standing,
            Fidelity = SightingFidelity.Detail,
            CampaignMapTileId = tileId,
            LastSeenPlayerTurnId = turn.Id,
            LastSeenTurnSequence = 2,
            StillVisible = true
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a Turn and records one whole engagement between the game's two players in it - commenced, a side giving way, and concluded - on the game's first campaign tile.
    /// </summary>
    /// <remarks>
    /// Written straight to the tables rather than through the battle lifecycle writer, for the same reason as <see cref="SeedSightingsAsync"/>: this fixture is about the purge reaching the rows.
    /// The departure and conclusion rows matter as well as the commencement, because they are the only kinds that name a player, so between them the rows reach every foreign key the feed has.
    /// </remarks>
    private async Task SeedBattleHistoryAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TurnQueue campaignQueue = await context.TurnQueues.SingleAsync(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null);
        int[] playerIds = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
        int victorId = playerIds[0];
        int routedId = playerIds[1];
        int tileId = await context.CampaignMaps.Where(t => t.GameId == gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var turn = new PlayerTurn
        {
            TurnQueueId = campaignQueue.Id,
            PlayerId = victorId,
            RoundNumber = campaignQueue.CurrentRoundNumber,
            StartedAt = now
        };

        // Concluded, with a victor, so the Battle row itself also names a player the purge has to outlast.
        var battle = new Battle
        {
            GameId = gameId,
            CampaignMapTileId = tileId,
            CommencedAtRoundNumber = campaignQueue.CurrentRoundNumber,
            ConcludedAtRoundNumber = campaignQueue.CurrentRoundNumber,
            OriginalTileStatus = TileOwnershipStatus.Unclaimed,
            VictorId = victorId,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = [],
            BattleParticipants =
            [
                .. playerIds.Take(2).Select(playerId => new BattleParticipant
                {
                    PlayerId = playerId,
                    JoinedAtRoundNumber = campaignQueue.CurrentRoundNumber,
                    IsStillInBattle = false,
                    LeftAtRoundNumber = campaignQueue.CurrentRoundNumber,
                    ExitReason = playerId == routedId ? BattleExitReason.Routed : BattleExitReason.CeaseFire,
                    RowVersion = []
                })
            ]
        };

        context.PlayerTurns.Add(turn);
        context.Battles.Add(battle);
        await context.SaveChangesAsync();

        context.BattleHistory.AddRange(
            new BattleHistory
            {
                GameId = gameId,
                BattleId = battle.Id,
                EventType = BattleHistoryEventType.Commenced,
                CampaignMapTileId = tileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 1
            },
            new BattleHistory
            {
                GameId = gameId,
                BattleId = battle.Id,
                EventType = BattleHistoryEventType.ParticipantLeft,
                ParticipantPlayerId = routedId,
                ExitReason = BattleExitReason.Routed,
                TroopsLost = 30,
                CampaignMapTileId = tileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 2
            },
            new BattleHistory
            {
                GameId = gameId,
                BattleId = battle.Id,
                EventType = BattleHistoryEventType.Concluded,
                GroundHeldByPlayerId = victorId,
                CampaignMapTileId = tileId,
                PlayerTurnId = turn.Id,
                TurnSequence = 3
            });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a Turn and gives one player an explored view of a one-tile battle map on the game's first campaign tile.
    /// </summary>
    /// <remarks>
    /// Written straight to the tables rather than through the generator and the engagement service, for the same reason as <see cref="SeedSightingsAsync"/>: this fixture is about the purge
    /// reaching the rows. The view names the Turn it was seen in, so it proves the purge takes the views before the player turns as well as before the battle map and the players.
    /// </remarks>
    private async Task SeedPlayerBattleMapAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TurnQueue campaignQueue = await context.TurnQueues.SingleAsync(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null);
        int playerId = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var campaignTile = await context.CampaignMaps.Where(t => t.GameId == gameId).OrderBy(t => t.CampaignMapId).Select(t => new { t.CampaignMapId, t.BiomeTypeId }).FirstAsync();

        // Battle map tile ids are application-assigned, so take the next one free.
        int battleTileId = (await context.BattleMap.MaxAsync(t => (int?)t.Id) ?? 0) + 1;

        var turn = new PlayerTurn
        {
            TurnQueueId = campaignQueue.Id,
            PlayerId = playerId,
            RoundNumber = campaignQueue.CurrentRoundNumber,
            StartedAt = DateTimeOffset.UtcNow
        };

        var battleMap = new BattleMapIndex
        {
            CampaignMapId = campaignTile.CampaignMapId,
            BattleMapOffsetId = battleTileId,
            GenerationStatus = MapCreationStatus.Complete,
            Status = "Generation complete"
        };

        context.PlayerTurns.Add(turn);
        context.BattleMapIndex.Add(battleMap);
        await context.SaveChangesAsync();

        context.BattleMap.Add(new BattleMap
        {
            Id = battleTileId,
            BattleMapIndexId = battleMap.Id,
            XCoord = 0,
            YCoord = 0,
            AltitudeLevel = (int)BattleMapAltitude.Ground,
            BiomeTypeId = campaignTile.BiomeTypeId
        });

        context.PlayerBattleMaps.Add(new PlayerBattleMap
        {
            PlayerId = playerId,
            BattleMapId = battleTileId,
            GameId = gameId,
            BattleMapIndexId = battleMap.Id,
            XCoord = 0,
            YCoord = 0,
            BiomeTypeId = campaignTile.BiomeTypeId,
            AltitudeLevel = (int)BattleMapAltitude.Ground,
            FirstSeenOnTurnId = turn.Id,
            LastSeenOnTurnId = turn.Id
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gives the game's first player two sustenance modifiers: one for every biome, and one naming the Grassland biome.
    /// </summary>
    /// <remarks>
    /// Written straight to the table, for the same reason as <see cref="SeedSightingsAsync"/>: nothing writes modifiers in play yet. The biome-specific row matters as well as the all-biome one,
    /// because it is the only kind that names a biome, so between them the rows reach every foreign key the table has.
    /// </remarks>
    private async Task SeedPlayerSustenanceModifiersAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int playerId = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();

        context.PlayerSustenanceModifiers.AddRange(
            new PlayerSustenanceModifier
            {
                GameId = gameId,
                PlayerId = playerId,
                SourceType = Domain.Enums.Modifiers.ModifierSourceType.Technology,
                SourceId = 1,
                Resource = Domain.Enums.Maps.SustenanceResource.Forage,
                BiomeTypeId = null,
                FlatValue = 50_000,
                PercentValue = 0
            },
            new PlayerSustenanceModifier
            {
                GameId = gameId,
                PlayerId = playerId,
                SourceType = Domain.Enums.Modifiers.ModifierSourceType.Technology,
                SourceId = 1,
                Resource = Domain.Enums.Maps.SustenanceResource.Hunt,
                BiomeTypeId = BiomeType.Grassland,
                FlatValue = 0,
                PercentValue = 250
            });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Records food taken from the game's first campaign map tile.
    /// </summary>
    /// <remarks>
    /// Written straight to the table rather than through the end-of-turn pipeline, which would need a started game with Forces on the map just to produce one row.
    /// </remarks>
    private async Task SeedCampaignMapDepletionAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int tileId = await context.CampaignMaps.Where(t => t.GameId == gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = gameId,
            ForageTaken = 66_667,
            HuntTaken = 33_333,
            FedOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gives the game one MapResource of each kind across its first two tiles, with a row in every per-Player MapResource table for its first player.
    /// </summary>
    /// <remarks>
    /// Written straight to the tables, for the same reason as <see cref="SeedPlayerSustenanceModifiersAsync"/>: nothing creates MapResources in a game yet. Between them the
    /// rows reach every foreign key the new tables have, so a purge step in the wrong order fails here.
    /// </remarks>
    private async Task SeedMapResourcesAsync(int gameId, MapResourceTestCatalogue catalogue)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int playerId = await context.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        List<int> tileIds = await context.CampaignMaps.Where(t => t.GameId == gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).Take(2).ToListAsync();

        // A renewable and a Distribution share the first tile; a stratified instance has the second to itself.
        var berry = new MapResourceInstance { GameId = gameId, CampaignMapId = tileIds[0], MapResourceTypeId = catalogue.BerryTypeId, Slot = 1, Quantity = 300_000, SpreadLimit = 3, EstimationFuzzyFactor = 0.2m };
        var wood = new MapResourceInstance { GameId = gameId, CampaignMapId = tileIds[0], MapResourceTypeId = catalogue.WoodTypeId, Slot = 2, EstimationFuzzyFactor = 0.1m, MapResourceDistributionScenarioId = catalogue.ScenarioId };
        var coal = new MapResourceInstance { GameId = gameId, CampaignMapId = tileIds[1], MapResourceTypeId = catalogue.CoalTypeId, Slot = 1 };

        context.MapResourceInstances.AddRange(berry, wood, coal);
        await context.SaveChangesAsync();

        context.MapResourceInstanceAmounts.Add(new MapResourceInstanceAmount { MapResourceInstanceId = wood.Id, AssetId = catalogue.AssetId, GameId = gameId, Amount = 10_000_000 });
        context.MapResourceInstanceLayers.Add(new MapResourceInstanceLayer { MapResourceInstanceId = coal.Id, LayerNumber = 1, GameId = gameId, AssetId = catalogue.AssetId, Quantity = 2_000_000, EstimationFuzzyFactor = 0.15m });
        context.PlayerKnownMapResources.Add(new PlayerKnownMapResource { GameId = gameId, PlayerId = playerId, MapResourceInstanceId = berry.Id, DiscoveredOnRound = 1 });
        context.PlayerMapResourceTypeKnowledge.Add(new PlayerMapResourceTypeKnowledge { PlayerId = playerId, MapResourceTypeId = catalogue.BerryTypeId, GameId = gameId, FirstSeenOnRound = 1 });

        context.PlayerMapResourceModifiers.Add(new PlayerMapResourceModifier
        {
            GameId = gameId,
            PlayerId = playerId,
            SourceType = Domain.Enums.Modifiers.ModifierSourceType.Technology,
            SourceId = 1,
            MapResourceTypeId = catalogue.BerryTypeId,
            Target = Domain.Enums.MapResources.MapResourceModifierTarget.GrowthPercentage,
            BiomeTypeId = BiomeType.Forest,
            FlatValue = 50,
            PercentValue = 0
        });

        context.PlayerMapResourceSwitches.Add(new PlayerMapResourceSwitch
        {
            GameId = gameId,
            PlayerId = playerId,
            SourceType = Domain.Enums.Modifiers.ModifierSourceType.Technology,
            SourceId = 1,
            Kind = Domain.Enums.MapResources.MapResourceSwitchKind.YieldAsset,
            MapResourceTypeId = catalogue.BerryTypeId,
            AssetId = catalogue.AssetId,
            IsEnabled = true
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Seeds a player-owned unit template in the given game, with one component row hanging off it.</summary>
    private async Task<int> SeedCustomTemplateAsync(int gameId, string displayName)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int playerId = await context.Players.Where(p => p.GameId == gameId).Select(p => p.Id).OrderBy(id => id).FirstAsync();
        UnitComponent body = await context.UnitComponents.FirstAsync(c => c.Code == "human-body");
        UnitComponentSlot slot = await GetOrAddSlotAsync(context, body.Id);

        var template = new UnitTemplate
        {
            // A custom template carries both ids; a global one carries neither. That is the whole distinction the purge turns on.
            GameId = gameId,
            PlayerId = playerId,
            DisplayName = displayName,
            BodyComponentId = body.Id,
            MapImagePath = "test.png"
        };

        context.UnitTemplates.Add(template);
        await context.SaveChangesAsync();

        context.UnitTemplateComponents.Add(new UnitTemplateComponent
        {
            UnitTemplateId = template.Id,
            UnitComponentSlotId = slot.Id,
            UnitComponentId = body.Id
        });

        await context.SaveChangesAsync();

        return template.Id;
    }

    /// <summary>
    /// Returns a component slot on the given body, creating one if the starter catalogue has none.
    /// </summary>
    /// <remarks>
    /// The base fixture's catalogue stops short of slots, but a template component row needs one, and that row is what proves the purge reaches a custom
    /// template's children as well as the template itself.
    /// </remarks>
    private static async Task<UnitComponentSlot> GetOrAddSlotAsync(AscendantEmpireContext context, int bodyComponentId)
    {
        UnitComponentSlot? existing = await context.UnitComponentSlots.FirstOrDefaultAsync();

        if (existing is not null)
        {
            return existing;
        }

        var slot = new UnitComponentSlot
        {
            Code = "test-slot",
            DisplayName = "Test Slot",
            UnitComponentBodyId = bodyComponentId,
            IsVisible = true
        };

        context.UnitComponentSlots.Add(slot);
        await context.SaveChangesAsync();

        return slot;
    }

    private async Task<int> SeedUserAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var user = new User
        {
            Username = $"account-{Guid.NewGuid():N}",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            InactiveReasonType = InactiveReasonType.Active
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task<int> SeedSessionAsync(int gameId, int playerId)
    {
        int userId = await SeedUserAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var session = new RefreshSession
        {
            UserId = userId,
            TokenHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            GameId = gameId,
            PlayerId = playerId
        };

        context.RefreshSessions.Add(session);
        await context.SaveChangesAsync();

        return session.Id;
    }

    /// <summary>
    /// Runs one raw statement against the test database, for the identity state the model has no way to express.
    /// </summary>
    private async Task ExecuteSqlAsync(string sql)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// The last identity value a table has handed out, or null when its identity has never been used.
    /// </summary>
    private async Task<long?> IdentityLastValueAsync(string table)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Interpolated into SqlQuery rather than SqlQueryRaw, so the table name travels as a parameter to OBJECT_ID rather than as text spliced into the statement.
        return await context.Database
            .SqlQuery<long?>($"SELECT CAST(last_value AS bigint) AS Value FROM sys.identity_columns WHERE object_id = OBJECT_ID({table})")
            .SingleAsync();
    }

    /// <summary>
    /// Inserts a bare GameIndex row and returns the id the database assigned it, which is how the identity counter is observed.
    /// </summary>
    private async Task<int> InsertBareGameAsync(string gameName)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            CampaignMapIndexId = _galleryIndexId,
            GameName = gameName,
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupPending,
            PlayStatus = GamePlayStatus.SetupInProgress,
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
}
