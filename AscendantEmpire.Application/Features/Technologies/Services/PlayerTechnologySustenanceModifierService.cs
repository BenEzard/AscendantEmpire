using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Linq.Expressions;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <inheritdoc cref="IPlayerTechnologySustenanceModifierService"/>
internal sealed class PlayerTechnologySustenanceModifierService(
    ILogger<PlayerTechnologySustenanceModifierService> logger
    ) : IPlayerTechnologySustenanceModifierService
{
    public async Task<int> ReconcileAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancellationToken)
    {
        GrantRow[] grants = await LoadGrantsAsync(context, cancellationToken);

        HashSet<int> discovered = await LoadDiscoveredAsync(context, playerId, cancellationToken);

        List<PlayerSustenanceModifier> held = await LoadHeldAsync(context, modifier => modifier.PlayerId == playerId, cancellationToken);

        return Reconcile(context, grants, discovered, gameId, playerId, held);
    }

    public async Task<int> ReconcileGameAsync(
        AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken)
    {
        GrantRow[] grants = await LoadGrantsAsync(context, cancellationToken);

        int[] playerIds = await context.Players
            .Where(player => player.GameId == gameId)
            .Select(player => player.Id)
            .ToArrayAsync(cancellationToken);

        var discoveredRows = await context.PlayerTechnologies
            .AsNoTracking()
            .Where(row => row.GameId == gameId && row.Status == TechnologyStatus.Discovered)
            .Select(row => new { row.PlayerId, row.TechnologyId })
            .ToArrayAsync(cancellationToken);

        List<PlayerSustenanceModifier> held = await LoadHeldAsync(context, modifier => modifier.GameId == gameId, cancellationToken);

        int changed = 0;

        foreach (int playerId in playerIds)
        {
            HashSet<int> discovered = [.. discoveredRows.Where(row => row.PlayerId == playerId).Select(row => row.TechnologyId)];

            changed += Reconcile(
                context,
                grants,
                discovered,
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
    /// The key is (<c>SourceId</c>, <c>Resource</c>, <c>BiomeTypeId</c>), matching the table's own unique index with <c>SourceType</c> fixed to Technology.
    /// Two technologies granting the same resource and biome therefore keep separate rows, which is what lets one of them be lost on its own.
    /// </remarks>
    private int Reconcile(
        AscendantEmpireContext context,
        GrantRow[] grants,
        HashSet<int> discovered,
        int gameId,
        int playerId,
        List<PlayerSustenanceModifier> held)
    {
        Dictionary<ModifierKey, GrantRow> wanted = grants
            .Where(grant => discovered.Contains(grant.TechnologyId))
            .ToDictionary(grant => new ModifierKey(grant.TechnologyId, grant.Resource, grant.BiomeTypeId));

        Dictionary<ModifierKey, PlayerSustenanceModifier> current = held
            .Where(modifier => modifier.SourceType == ModifierSourceType.Technology)
            .ToDictionary(modifier => new ModifierKey(modifier.SourceId, modifier.Resource, modifier.BiomeTypeId));

        int added = 0;
        int updated = 0;
        int removed = 0;

        foreach ((ModifierKey key, GrantRow grant) in wanted)
        {
            if (!current.TryGetValue(key, out PlayerSustenanceModifier? modifier))
            {
                context.PlayerSustenanceModifiers.Add(new PlayerSustenanceModifier
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    SourceType = ModifierSourceType.Technology,
                    SourceId = grant.TechnologyId,
                    Resource = grant.Resource,
                    BiomeTypeId = grant.BiomeTypeId,
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

        foreach ((ModifierKey key, PlayerSustenanceModifier modifier) in current)
        {
            if (wanted.ContainsKey(key))
            {
                continue;
            }

            context.PlayerSustenanceModifiers.Remove(modifier);

            removed++;
        }

        if (added + updated + removed > 0)
        {
            logger.LogInformation(
                "Reconciled technology sustenance modifiers for player {PlayerId} in game {GameId}: {AddedCount} added, {UpdatedCount} updated, {RemovedCount} removed.",
                playerId,
                gameId,
                added,
                updated,
                removed);
        }

        return added + updated + removed;
    }

    /// <summary>
    /// Loads the whole grant catalogue, which is a handful of rows per technology and the same for every Player in the game.
    /// </summary>
    private static async Task<GrantRow[]> LoadGrantsAsync(AscendantEmpireContext context, CancellationToken cancellationToken)
    {
        return await context.TechnologySustenanceModifierGrants
            .AsNoTracking()
            .Select(grant => new GrantRow(grant.TechnologyId, grant.Resource, grant.BiomeTypeId, grant.FlatValue, grant.PercentValue))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<HashSet<int>> LoadDiscoveredAsync(
        AscendantEmpireContext context,
        int playerId,
        CancellationToken cancellationToken)
    {
        int[] technologyIds = await context.PlayerTechnologies
            .AsNoTracking()
            .Where(row => row.PlayerId == playerId && row.Status == TechnologyStatus.Discovered)
            .Select(row => row.TechnologyId)
            .ToArrayAsync(cancellationToken);

        return [.. technologyIds];
    }

    /// <summary>
    /// Loads the Player's modifier rows tracked, since the reconcile updates and removes them.
    /// </summary>
    private static async Task<List<PlayerSustenanceModifier>> LoadHeldAsync(
        AscendantEmpireContext context,
        Expression<Func<PlayerSustenanceModifier, bool>> scope,
        CancellationToken cancellationToken)
    {
        return await context.PlayerSustenanceModifiers
            .Where(scope)
            .Where(modifier => modifier.SourceType == ModifierSourceType.Technology)
            .ToListAsync(cancellationToken);
    }

    private readonly record struct ModifierKey(int TechnologyId, SustenanceResource Resource, BiomeType? BiomeTypeId);

    private sealed record GrantRow(int TechnologyId, SustenanceResource Resource, BiomeType? BiomeTypeId, long FlatValue, long PercentValue);
}
