using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Linq.Expressions;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <inheritdoc cref="IPlayerTechnologyExtractionEfficiencyService"/>
internal sealed class PlayerTechnologyExtractionEfficiencyService(
    ILogger<PlayerTechnologyExtractionEfficiencyService> logger
    ) : IPlayerTechnologyExtractionEfficiencyService
{
    public async Task<int> ReconcileAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken)
    {
        GrantRow[] grants = await LoadGrantsAsync(context, cancellationToken);

        List<PlayerTechnology> technologyRows = await LoadTechnologyRowsAsync(context, row => row.PlayerId == playerId, cancellationToken);

        List<PlayerExtractionEfficiencyModifier> held = await LoadHeldAsync(context, modifier => modifier.PlayerId == playerId, cancellationToken);

        return Reconcile(context, grants, DiscoveredIn(technologyRows), gameId, playerId, held);
    }

    public async Task<int> ReconcileGameAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)
    {
        GrantRow[] grants = await LoadGrantsAsync(context, cancellationToken);

        int[] playerIds = await context.Players
            .Where(player => player.GameId == gameId)
            .Select(player => player.Id)
            .ToArrayAsync(cancellationToken);

        List<PlayerTechnology> technologyRows = await LoadTechnologyRowsAsync(context, row => row.GameId == gameId, cancellationToken);

        List<PlayerExtractionEfficiencyModifier> held = await LoadHeldAsync(context, modifier => modifier.GameId == gameId, cancellationToken);

        int changed = 0;

        foreach (int playerId in playerIds)
        {
            changed += Reconcile(
                context,
                grants,
                DiscoveredIn([.. technologyRows.Where(row => row.PlayerId == playerId)]),
                gameId,
                playerId,
                [.. held.Where(modifier => modifier.PlayerId == playerId)]);
        }

        return changed;
    }

    /// <summary>
    /// Diffs one Player's held rows against the grants their discovered technologies carry.
    /// </summary>
    /// <remarks>
    /// The key is (<c>SourceId</c>, <c>AssetId</c>), matching the table's own unique index with <c>SourceType</c> fixed to Technology, so two technologies granting
    /// the same Asset keep separate rows and either can be lost on its own.
    /// </remarks>
    private int Reconcile(
        AscendantEmpireContext context,
        GrantRow[] grants,
        HashSet<int> discovered,
        int gameId,
        int playerId,
        List<PlayerExtractionEfficiencyModifier> held)
    {
        Dictionary<ModifierKey, GrantRow> wanted = grants
            .Where(grant => discovered.Contains(grant.TechnologyId))
            .ToDictionary(grant => new ModifierKey(grant.TechnologyId, grant.AssetId));

        Dictionary<ModifierKey, PlayerExtractionEfficiencyModifier> current = held
            .Where(modifier => modifier.SourceType == ModifierSourceType.Technology)
            .ToDictionary(modifier => new ModifierKey(modifier.SourceId, modifier.AssetId));

        int added = 0;
        int updated = 0;
        int removed = 0;

        foreach ((ModifierKey key, GrantRow grant) in wanted)
        {
            if (!current.TryGetValue(key, out PlayerExtractionEfficiencyModifier? modifier))
            {
                context.PlayerExtractionEfficiencyModifiers.Add(new PlayerExtractionEfficiencyModifier
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    SourceType = ModifierSourceType.Technology,
                    SourceId = grant.TechnologyId,
                    AssetId = grant.AssetId,
                    FlatValue = grant.FlatValue,
                    PercentValue = grant.PercentValue
                });

                added++;

                continue;
            }

            if (modifier.FlatValue == grant.FlatValue && modifier.PercentValue == grant.PercentValue)
            {
                continue;
            }

            modifier.FlatValue = grant.FlatValue;
            modifier.PercentValue = grant.PercentValue;

            updated++;
        }

        foreach ((ModifierKey key, PlayerExtractionEfficiencyModifier modifier) in current)
        {
            if (wanted.ContainsKey(key))
            {
                continue;
            }

            context.PlayerExtractionEfficiencyModifiers.Remove(modifier);

            removed++;
        }

        if (added + updated + removed > 0)
        {
            logger.LogInformation(
                "Reconciled technology extraction efficiency modifiers for player {PlayerId} in game {GameId}: {AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed.",
                playerId,
                gameId,
                added,
                updated,
                removed);
        }

        return added + updated + removed;
    }

    private static async Task<GrantRow[]> LoadGrantsAsync(AscendantEmpireContext context, CancellationToken cancellationToken)
    {
        return await context.TechnologyExtractionEfficiencyGrants
            .AsNoTracking()
            .Select(grant => new GrantRow(grant.TechnologyId, grant.AssetId, grant.FlatValue, grant.PercentValue))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the technology rows in scope, tracked and with no status filter.
    /// </summary>
    /// <remarks>
    /// The callers reconcile with the discovery that triggered them still pending on a tracked entity, so the Discovered filter is applied in memory, where that
    /// change is visible - see <c>PlayerTechnologySustenanceModifierService</c>, which has the same shape for the same reason.
    /// </remarks>
    private static async Task<List<PlayerTechnology>> LoadTechnologyRowsAsync(
        AscendantEmpireContext context,
        Expression<Func<PlayerTechnology, bool>> scope,
        CancellationToken cancellationToken)
    {
        return await context.PlayerTechnologies
            .Where(scope)
            .ToListAsync(cancellationToken);
    }

    private static HashSet<int> DiscoveredIn(List<PlayerTechnology> technologyRows)
    {
        return [.. technologyRows
            .Where(row => row.Status == TechnologyStatus.Discovered)
            .Select(row => row.TechnologyId)];
    }

    /// <summary>
    /// Loads the Player's technology-sourced modifier rows tracked, since the reconcile updates and removes them.
    /// </summary>
    /// <remarks>
    /// Rows added but not yet saved are invisible to a query, <b>so call this service at most once per Player per unit of work</b>, or the second call adds what the
    /// first already added and the unique index refuses the save.
    /// </remarks>
    private static async Task<List<PlayerExtractionEfficiencyModifier>> LoadHeldAsync(
        AscendantEmpireContext context,
        Expression<Func<PlayerExtractionEfficiencyModifier, bool>> scope,
        CancellationToken cancellationToken)
    {
        return await context.PlayerExtractionEfficiencyModifiers
            .Where(scope)
            .Where(modifier => modifier.SourceType == ModifierSourceType.Technology)
            .ToListAsync(cancellationToken);
    }

    private readonly record struct ModifierKey(int TechnologyId, int AssetId);

    private sealed record GrantRow(int TechnologyId, int AssetId, long FlatValue, long PercentValue);
}
