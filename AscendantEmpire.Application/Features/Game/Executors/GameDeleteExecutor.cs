using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Game.Classes;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Game.Executors;

/// <summary>
/// Executes <see cref="ProcessType.DeleteGame"/> jobs — permanently removes one Game and every row scoped to it.
/// </summary>
/// <remarks>
/// <para>
/// Nothing cascades. Every game-scoped foreign key in <c>AscendantEmpireContext</c> is mapped <c>DeleteBehavior.NoAction</c>, so the purge is a hand-ordered
/// sequence of bulk deletes: children before parents, and specifically battle maps before the campaign map they hang off, because that one relationship is
/// <c>Restrict</c> and the database will refuse the delete outright rather than let it through.
/// </para>
/// <para>
/// The whole purge runs in one transaction, so a failure part way through leaves the Game intact rather than half-eaten.
/// </para>
/// <para>
/// <b>What survives.</b> Everything that is not scoped to this Game: the biome, geography, structure, resource and unit-component catalogues, the map
/// gallery (including its start positions, which are authored per gallery map rather than per game), user accounts and their permissions, and the global
/// unit templates. A global template is one whose <c>GameId</c> and <c>PlayerId</c> are both null; only templates belonging to this Game or one of its
/// players are removed. That distinction matters — a predicate of "has a GameId or a PlayerId", which the old wipe-everything script used, would take every
/// other Game's custom templates with it.
/// </para>
/// </remarks>
internal sealed class GameDeleteExecutor(
    AscendantEmpireContext context,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IRefreshSessionService refreshSessions,
    ILogger<GameDeleteExecutor> logger
) : IProcessExecutor
{
    /// <summary>
    /// Progress is reserved either side of the delete steps: this much before the first one, and the rest of the way to 100 after the last.
    /// </summary>
    private const int _PROGRESS_BEFORE_DELETES = 5;
    private const int _PROGRESS_AFTER_DELETES = 95;

    /// <summary>
    /// The IDENTITY tables whose counters are reset when the last Game in the database is deleted, so a fresh start numbers everything from 1 again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every table here is wholly game-scoped, so once no Game remains it is necessarily empty — but each reseed is still guarded by its own emptiness
    /// check, because a reseed against a table that unexpectedly still holds rows would hand out ids that collide with them.
    /// </para>
    /// <para>
    /// Deliberately absent: <c>unitdesigns</c> tables, which keep their global catalogue rows and so are never empty; <c>battle.BattleParticipant</c>, which
    /// is keyed on its battle and player rather than on an identity; <c>game.PlayerBattleMap</c>, which is keyed on its player and battle map tile; and <c>game.CampaignMap</c>, <c>game.PlayerCampaignMap</c> and
    /// <c>battle.BattleMap</c>, whose ids are assigned by the application rather than by an IDENTITY column. Those last three allocate each new block as
    /// <c>MAX(id) + 1</c>, so an empty table restarts at 1 with no help from us.
    /// </para>
    /// </remarks>
    private static readonly string[] _IDENTITY_TABLES_TO_RESEED =
    [
        "[units].[MovementHistory]",
        "[structure].[StructureHistory]",
        "[battle].[BattleHistory]",
        "[units].[ForceOrderParticipants]",
        "[units].[ForceOrderMovementSteps]",
        "[units].[ForceOrders]",
        "[units].[UnitInstances]",
        "[units].[Commands]",
        "[game].[PlayerReport]",
        "[game].[PlayerKnownForce]",
        "[game].[PlayerKnownStructure]",
        "[game].[StructureInstances]",
        "[turns].[PlayerTurn]",
        "[turns].[TurnQueueParticipant]",
        "[turns].[TurnQueue]",
        "[battle].[Battle]",
        "[battle].[BattleMapIndex]",
        "[military].[PlayerOrganisationStructureTier]",
        "[military].[PlayerOrganisationStructure]",
        "[military].[PlayerOfficerRank]",
        "[game].[PlayerSustenanceModifier]",
        "[game].[PlayerKnownMapResource]",
        "[game].[PlayerMapResourceSwitch]",
        "[game].[PlayerMapResourceModifier]",
        "[game].[MapResourceInstance]",
        "[game].[Player]",
        "[game].[GameIndex]",
    ];

    public ProcessType ProcessType => ProcessType.DeleteGame;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = _PROGRESS_BEFORE_DELETES;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            int gameId = GameDeleteMetadata.TryRead(processing.MetadataJson)
                ?? throw new InvalidOperationException("Processing record does not carry the id of the game to delete.");

            logger.LogInformation("Starting delete of game {GameId} for ProcessingId {ProcessingId}.", gameId, processingId);

            // Kick anyone still holding a session scoped to this game before its rows start disappearing underneath them.
            int revokedSessions = await refreshSessions.RevokeByGameAsync(gameId, cancellationToken);

            (int gamesRemaining, int rowsDeleted) = await PurgeGameAsync(gameId, processing, cancellationToken);

            // Reseeding is only safe once nothing is left to collide with, so it waits for the very last game to go.
            bool countersReset = false;
            if (gamesRemaining == 0)
            {
                countersReset = true;
                await ReseedIdentityCountersAsync(cancellationToken);
            }

            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = BuildSummary(rowsDeleted, revokedSessions, countersReset);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Delete of game {GameId} complete for ProcessingId {ProcessingId}. {Summary}",
                gameId, processingId, processing.ResultReference);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Game delete failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Deletes the Game and everything scoped to it, in one transaction.
    /// </summary>
    /// <returns>How many Games are left in the database afterwards, and how many rows the purge removed in total.</returns>
    /// <remarks>
    /// Runs on its own context, separate from the one tracking the <see cref="Processing"/> record, so progress updates are committed as they happen rather
    /// than being trapped inside this transaction and only appearing once the whole purge lands.
    /// </remarks>
    private async Task<(int GamesRemaining, int RowsDeleted)> PurgeGameAsync(
        int gameId,
        Processing processing,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext purge = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using IDbContextTransaction transaction = await purge.Database.BeginTransactionAsync(cancellationToken);

        // --- Id sets the delete steps filter on ---------------------------------------------------
        // Resolved up front because the rows they come from are themselves deleted before the steps that need them have all run.
        //
        // Players and custom templates are few, so they are read into memory; the campaign map's tile ids are not — a large map runs to hundreds of
        // thousands of tiles — so that one stays an IQueryable and translates into a subquery instead.
        List<int> playerIds = await purge.Players
            .Where(p => p.GameId == gameId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        List<int> templateIds = await purge.UnitTemplates
            .Where(t => t.GameId == gameId || (t.PlayerId != null && playerIds.Contains(t.PlayerId.Value)))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        List<int> legacyDesignIds = await purge.UnitDesigns
            .Where(d => d.GameId == gameId || (d.PlayerId != null && playerIds.Contains(d.PlayerId.Value)))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        IQueryable<int> tileIds = purge.CampaignMaps
            .Where(t => t.GameId == gameId)
            .Select(t => t.CampaignMapId);

        // --- The ordered purge --------------------------------------------------------------------
        // Each entry is one bulk delete. The order is forced by the foreign keys; the comments call out the ones that are not obvious.
        List<(string Label, Func<Task<int>> Delete)> steps =
        [
            // Orders and their pieces go before the units and commands they point at.
            ("force order participants", () => purge.ForceOrderParticipants
                .Where(p => purge.ForceOrders.Any(o => o.GameId == gameId && o.Id == p.ForceOrderId)
                         || purge.UnitInstances.Any(u => u.GameId == gameId && u.Id == p.UnitInstanceId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("force order movement steps", () => purge.ForceOrderMovementSteps
                .Where(s => purge.ForceOrders.Any(o => o.GameId == gameId && o.Id == s.ForceOrderId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("force orders", () => purge.ForceOrders
                .Where(o => o.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Movement history names tiles, players, templates and player turns, so it goes before every one of them. It carries its own GameId and so needs no scoping through a parent. Its
            // UnitInstanceId is deliberately not a foreign key, so its place relative to the units themselves does not matter to the database.
            ("movement history", () => purge.MovementHistory
                .Where(h => h.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Structure history names structures, tiles, players and player turns, so it goes before all of them too. Unlike movement history its instance id is a real foreign key, which this
            // position already satisfies: structures are purged well below.
            ("structure history", () => purge.StructureHistory
                .Where(h => h.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Battle history, the third raw feed, names battles, players, tiles and player turns, so it goes at the same point as the other two - and before the battles especially, since its
            // BattleId is a real foreign key. It carries its own GameId, so needs no scoping through the battle.
            ("battle history", () => purge.BattleHistory
                .Where(h => h.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // The intelligence feed names tiles, players, templates and player turns just as movement history does, so it goes at the same point. Path tiles before their reports, since that
            // relationship is NoAction too; like the history, neither table's Force id is a foreign key.
            ("player report path tiles", () => purge.PlayerReportPathTiles
                .Where(t => purge.PlayerReports.Any(r => r.GameId == gameId && r.Id == t.PlayerReportId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("player reports", () => purge.PlayerReports
                .Where(r => r.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("player known forces", () => purge.PlayerKnownForces
                .Where(k => k.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Each battle participant's view of a battle map names the players, the player turns they first and last saw a tile in, and the battle map tiles and indexes, so it goes before all
            // of them. It carries its own GameId, so needs no scoping through the campaign map the way the battle maps themselves do.
            ("player battle map views", () => purge.PlayerBattleMaps
                .Where(v => v.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Units carry a CommandId, so they go before the commands.
            ("units", () => purge.UnitInstances
                .Where(u => u.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("commands", () => purge.Commands
                .Where(c => c.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // What players have seen of a structure goes before the structure itself. So do the structure reports above, whose subject is a real foreign key to it.
            ("player known structures", () => purge.PlayerKnownStructures
                .Where(s => s.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("structures", () => purge.StructureInstances
                .Where(s => s.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Turn queues carry a BattleId, so they go before the battles.
            //
            // Player turns hang off the queue rather than the game, so they are scoped through it - the same shape as the participants below, and for the
            // same reason.
            ("player turns", () => purge.PlayerTurns
                .Where(t => purge.TurnQueues.Any(q => q.GameId == gameId && q.Id == t.TurnQueueId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("turn queue participants", () => purge.TurnQueueParticipants
                .Where(p => purge.TurnQueues.Any(q => q.GameId == gameId && q.Id == p.TurnQueueId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("turn queues", () => purge.TurnQueues
                .Where(q => q.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("battle participants", () => purge.BattleParticipants
                .Where(p => purge.Battles.Any(b => b.GameId == gameId && b.Id == p.BattleId))
                .ExecuteDeleteAsync(cancellationToken)),

            // Battles name a victor and an original tile owner, so they go before the players.
            ("battles", () => purge.Battles
                .Where(b => b.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("battle map tiles", () => purge.BattleMap
                .Where(t => purge.BattleMapIndex.Any(i => tileIds.Contains(i.CampaignMapId) && i.Id == t.BattleMapIndexId))
                .ExecuteDeleteAsync(cancellationToken)),

            // The one Restrict relationship in the whole purge: the database refuses to drop a campaign map tile while a battle map still indexes it.
            ("battle map indexes", () => purge.BattleMapIndex
                .Where(i => tileIds.Contains(i.CampaignMapId))
                .ExecuteDeleteAsync(cancellationToken)),

            // Per-player unlock and design state. The unlocked-template rows include ones pointing at global templates that survive; the row is still
            // this player's and still goes.
            ("player template unlocks", () => purge.PlayerUnlockedTemplates
                .Where(u => playerIds.Contains(u.PlayerId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("player component unlocks", () => purge.PlayerUnlockedComponents
                .Where(u => playerIds.Contains(u.PlayerId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("player design states", () => purge.PlayerUnitDesignStates
                .Where(s => playerIds.Contains(s.PlayerId))
                .ExecuteDeleteAsync(cancellationToken)),

            // The legacy design system. Nothing writes to these tables any more, but rows from older games are still in them.
            ("legacy design stats", () => purge.UnitDesignStats
                .Where(s => legacyDesignIds.Contains(s.UnitDesignId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("legacy designs", () => purge.UnitDesigns
                .Where(d => legacyDesignIds.Contains(d.Id))
                .ExecuteDeleteAsync(cancellationToken)),

            // Player-owned templates and everything hanging off them. Global templates are untouched: they have no GameId and no PlayerId, so they are
            // not in templateIds in the first place.
            ("template supersessions", () => purge.TemplateSupersessions
                .Where(s => templateIds.Contains(s.PredecessorTemplateId) || templateIds.Contains(s.SuccessorTemplateId))
                .ExecuteDeleteAsync(cancellationToken)),

            // A compiled template shares its primary key with the template it was compiled from, so its children filter on the same ids.
            ("compiled template capabilities", () => purge.CompiledUnitTemplateCapabilities
                .Where(c => templateIds.Contains(c.CompiledUnitTemplateId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("compiled template components", () => purge.CompiledUnitTemplateComponents
                .Where(c => templateIds.Contains(c.CompiledUnitTemplateId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("compiled templates", () => purge.CompiledUnitTemplates
                .Where(c => templateIds.Contains(c.UnitTemplateId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("template components", () => purge.UnitTemplateComponents
                .Where(c => templateIds.Contains(c.UnitTemplateId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("custom templates", () => purge.UnitTemplates
                .Where(t => templateIds.Contains(t.Id))
                .ExecuteDeleteAsync(cancellationToken)),

            // Per-player military organisation. Tiers hang off structures; the Tag a structure names is catalogue data and survives.
            ("organisation tiers", () => purge.PlayerOrganisationStructureTiers
                .Where(t => purge.PlayerOrganisationStructures.Any(s => playerIds.Contains(s.PlayerId) && s.Id == t.StructureId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("organisation structures", () => purge.PlayerOrganisationStructures
                .Where(s => playerIds.Contains(s.PlayerId))
                .ExecuteDeleteAsync(cancellationToken)),

            ("officer ranks", () => purge.PlayerOfficerRanks
                .Where(r => playerIds.Contains(r.PlayerId))
                .ExecuteDeleteAsync(cancellationToken)),

            // Per-player sustenance modifiers name the players, so they go before them. The biomes they also name are catalogue data and survive. They carry their own GameId, so need no
            // scoping through the players.
            ("player sustenance modifiers", () => purge.PlayerSustenanceModifiers
                .Where(m => m.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // The per-player MapResource tables name the players, so they go before them; the discoveries also name the instances, so they go before those too. The
            // MapResourceTypes, Resources, Biomes and Geographies they name are catalogue data and survive. All carry their own GameId, so need no scoping through a parent.
            ("player known map resources", () => purge.PlayerKnownMapResources
                .Where(k => k.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("player map resource switches", () => purge.PlayerMapResourceSwitches
                .Where(s => s.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("player map resource modifiers", () => purge.PlayerMapResourceModifiers
                .Where(m => m.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("player map resource type knowledge", () => purge.PlayerMapResourceTypeKnowledge
                .Where(k => k.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // A Distribution's amounts and a stratified instance's layers name their instance, so they go first; the instances name the tiles, so they go before the
            // campaign map.
            ("map resource instance amounts", () => purge.MapResourceInstanceAmounts
                .Where(a => a.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("map resource instance layers", () => purge.MapResourceInstanceLayers
                .Where(l => l.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("map resource instances", () => purge.MapResourceInstances
                .Where(i => i.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // Campaign map depletion names the tiles, so it goes before the campaign map. It carries its own GameId, so needs no scoping through the tiles, and its key is the
            // tile id rather than an identity, so there is nothing to reseed.
            ("campaign map depletion", () => purge.CampaignMapDepletions
                .Where(d => d.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            // The maps, then the players, then the game itself. Campaign map tiles name the player who controls, first saw and last visited them, so
            // they have to go before the players do.
            ("player map views", () => purge.PlayerCampaignMaps
                .Where(t => t.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("campaign map", () => purge.CampaignMaps
                .Where(t => t.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("players", () => purge.Players
                .Where(p => p.GameId == gameId)
                .ExecuteDeleteAsync(cancellationToken)),

            ("game", () => purge.GameIndex
                .Where(g => g.Id == gameId)
                .ExecuteDeleteAsync(cancellationToken)),
        ];

        int rowsDeleted = 0;

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            (string label, Func<Task<int>> delete) = steps[stepIndex];

            int rows = await delete();
            rowsDeleted += rows;

            logger.LogDebug("Game {GameId}: deleted {RowCount} {Label}.", gameId, rows, label);

            await ReportProgressAsync(processing, stepIndex + 1, steps.Count, cancellationToken);
        }

        // Refresh sessions have no foreign key to a game or a player, so nothing else would ever clear these columns and they would be left pointing at
        // rows that no longer exist. The sessions themselves are kept — they are the audit trail of who was logged in.
        await purge.RefreshSessions
            .Where(s => s.GameId == gameId || (s.PlayerId != null && playerIds.Contains(s.PlayerId.Value)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.GameId, (int?)null).SetProperty(x => x.PlayerId, (int?)null), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        int gamesRemaining = await purge.GameIndex.CountAsync(cancellationToken);

        return (gamesRemaining, rowsDeleted);
    }

    /// <summary>
    /// Resets the IDENTITY counter of every wholly game-scoped table that is now empty, so the next game starts numbering from 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called only when the deleted Game was the last one. Each statement carries its own guards rather than relying on that call site: a table that somehow
    /// still holds rows is skipped instead of being handed ids that clash with them, and one whose key has since stopped being an identity is skipped rather
    /// than failing the whole job. Runs after the purge transaction has committed — a reseed is not something to leave sitting inside a transaction that
    /// might yet roll back.
    /// </para>
    /// <para>
    /// <b>A table nothing has ever been inserted into is left alone.</b> <c>DBCC CHECKIDENT(..., RESEED, 0)</c> normally means "the next row gets 1", but on a
    /// table whose identity has never been used SQL Server hands the reseed value itself to the next row — so reseeding one of those would deal out id 0. A
    /// table in that state already numbers from its seed, so there is nothing to reset.
    /// </para>
    /// </remarks>
    private async Task ReseedIdentityCountersAsync(CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext reseed = await dbFactory.CreateDbContextAsync(cancellationToken);

        foreach (string table in _IDENTITY_TABLES_TO_RESEED)
        {
            // The table names are compile-time constants from the array above, never user input.
            //
            // The last_value test is both guards in one: a row in sys.identity_columns means the table still has an identity to reset, and a non-null
            // last_value means that identity has actually handed out a value. A null one - a table created by a migration and never inserted into - must be
            // left alone, because SQL Server gives the reseed value itself to the first row of a table in that state, which would start it at id 0.
            string sql = $"""
                IF NOT EXISTS (SELECT 1 FROM {table})
                   AND EXISTS (SELECT 1 FROM sys.identity_columns WHERE object_id = OBJECT_ID('{table}') AND last_value IS NOT NULL)
                BEGIN
                    DBCC CHECKIDENT('{table}', RESEED, 0) WITH NO_INFOMSGS;
                END
                """;

            await reseed.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        logger.LogInformation("Last game deleted; identity counters reset across {TableCount} tables.", _IDENTITY_TABLES_TO_RESEED.Length);
    }

    /// <summary>
    /// Persists how far through the delete steps the purge has got, scaled into the band reserved for them.
    /// </summary>
    private async Task ReportProgressAsync(Processing processing, int stepsDone, int stepCount, CancellationToken cancellationToken)
    {
        int span = _PROGRESS_AFTER_DELETES - _PROGRESS_BEFORE_DELETES;

        processing.ProgressPercent = (byte)(_PROGRESS_BEFORE_DELETES + (span * stepsDone / stepCount));

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a short summary of what the purge removed, for display when the job completes.
    /// </summary>
    private static string BuildSummary(int rowsDeleted, int revokedSessions, bool countersReset)
    {
        string summary = $"Deleted {rowsDeleted} row(s) and revoked {revokedSessions} session(s).";

        return countersReset ? $"{summary} That was the last game, so id counters were reset." : summary;
    }
}
