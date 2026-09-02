using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <inheritdoc cref="IForceActionContextBuilder"/>
internal sealed class ForceActionContextBuilder(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IForceActionContextBuilder
{
    public async Task<IReadOnlyCollection<ForceActionContext>?> BuildAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNull(forceIds);

        // The same Force listed twice is one Force. Deduplicating here rather than trusting the caller is what keeps the count check below honest:
        // with duplicates left in, a selection of [7, 7] would compare one projected row against two requested ids and be rejected as incomplete.
        List<int> requestedForceIds = [.. forceIds.Distinct()];

        if (requestedForceIds.Count == 0)
        {
            return null;
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // The selected Forces are fetched on their own rather than as part of the tile query, so that "no such Force" and "the Force is there but
        // it isn't yours" can be told apart from "the tile is empty" by the caller.
        List<ForceActionForce> selectedForces = await ProjectForces(context)
            .Where(force => requestedForceIds.Contains(force.UnitInstanceId))
            .ToListAsync(cancelToken);

        // All-or-nothing: a selection is answerable only if every Force in it exists, is still standing and belongs to the caller. A short count
        // covers "no such id", "in another game" and "wiped out" together, exactly as the single-Force build always did.
        if (selectedForces.Count != requestedForceIds.Count || selectedForces.Exists(force => force.PlayerId != playerId))
        {
            return null;
        }

        // Every tile the selection stands on. The selection bar can only offer Forces from one tile today, so this is virtually always a single id —
        // but grouping by tile costs nothing here and means a cross-tile selection would need no change to this builder.
        List<int> occupiedTileIds = [.. selectedForces.Select(force => force.CampaignMapTileId).Distinct()];

        // Only the player's own Forces are gathered. Including enemy Forces would need the vision rules applied first, or the toolbar would
        // quietly confirm what is standing on a tile the player cannot see — see the design doc's open questions.
        List<ForceActionForce> forcesOnOccupiedTiles = await ProjectForces(context)
            .Where(force => occupiedTileIds.Contains(force.CampaignMapTileId))
            .Where(force => force.PlayerId == playerId)
            .ToListAsync(cancelToken);

        // Two queries, whatever the size of the selection. That fixed cost is the promise the pure-strategy design rests on: the toolbar's price is
        // set by the pipeline, not by how many Forces the player selected or how many action types exist.
        Dictionary<int, List<ForceActionForce>> forcesByTileId = forcesOnOccupiedTiles
            .GroupBy(force => force.CampaignMapTileId)
            .ToDictionary(tile => tile.Key, tile => tile.ToList());

        // Ordered by Force id so the reasons an action comes back unavailable arrive in a stable order, however the caller happened to list the ids.
        // Without it, two requests selecting the same Forces could reshuffle the reasons in the tooltip.
        return
        [
            .. selectedForces
                .OrderBy(force => force.UnitInstanceId)
                .Select(force => new ForceActionContext
                {
                    SelectedForce = force,
                    OtherForcesOnTile = forcesByTileId.TryGetValue(force.CampaignMapTileId, out List<ForceActionForce>? tileMates)
                        ? [.. tileMates.Where(other => other.UnitInstanceId != force.UnitInstanceId)]
                        : []
                })
        ];

        // Shared projection so the selected Forces and their neighbours are always shaped identically.
        // Everything the strategies read is flattened off the compiled snapshot here, so no EF entity escapes into the pure rule layer.
        IQueryable<ForceActionForce> ProjectForces(AscendantEmpireContext dbContext)
        {
            return dbContext.UnitInstances
                .AsNoTracking()
                .Where(force => force.GameId == gameId)
                .Where(force => force.HealthyCount + force.InjuredCount > 0) // A wiped-out force has no actions to offer.
                .Select(force => new ForceActionForce
                {
                    UnitInstanceId = force.Id,
                    PlayerId = force.PlayerId,
                    CampaignMapTileId = force.CampaignMapTileId,
                    // The compiled snapshot always exists: a Force cannot be spawned from an uncompiled template.
                    DisplayName = force.Name ?? force.UnitTemplate.Compiled!.DisplayName,
                    HealthyCount = force.HealthyCount,
                    InjuredCount = force.InjuredCount,
                    MovementPointsRemaining = force.MovementPointsRemaining,
                    CampaignMovementPoints = force.UnitTemplate.Compiled!.CampaignMovementPoints,
                    Organisation = force.UnitTemplate.Compiled!.OrganisationTag == null
                        ? null
                        : new ForceActionOrganisation
                        {
                            TagId = force.UnitTemplate.Compiled!.OrganisationTag!.Id,
                            Code = force.UnitTemplate.Compiled!.OrganisationTag!.Code,
                            DisplayName = force.UnitTemplate.Compiled!.OrganisationTag!.DisplayName,
                            LeadershipModel = force.UnitTemplate.Compiled!.OrganisationTag!.LeadershipModel,
                            UnledBaseSize = force.UnitTemplate.Compiled!.OrganisationTag!.UnledBaseSize,
                            UnledMaxSize = force.UnitTemplate.Compiled!.OrganisationTag!.UnledMaxSize
                        }
                });
        }
    }
}
