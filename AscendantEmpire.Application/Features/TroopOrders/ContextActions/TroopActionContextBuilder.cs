using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.TroopOrders.ContextActions;

/// <inheritdoc cref="ITroopActionContextBuilder"/>
internal sealed class TroopActionContextBuilder(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : ITroopActionContextBuilder
{
    public async Task<TroopActionContext?> BuildAsync(int gameId, int playerId, int unitInstanceId, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // The selected troop is fetched on its own rather than as part of the tile query, so that "no such troop" and "the troop is there but
        // it isn't yours" can be told apart from "the tile is empty" by the caller.
        ForceActionForce? selectedTroop = await ProjectTroops(context)
            .Where(troop => troop.UnitInstanceId == unitInstanceId)
            .FirstOrDefaultAsync(cancelToken);

        if (selectedTroop is null || selectedTroop.PlayerId != playerId)
        {
            return null;
        }

        // Only the player's own troops are gathered. Including enemy troops would need the vision rules applied first, or the toolbar would
        // quietly confirm what is standing on a tile the player cannot see — see the design doc's open questions.
        List<ForceActionForce> otherTroopsOnTile = await ProjectTroops(context)
            .Where(troop => troop.CampaignMapTileId == selectedTroop.CampaignMapTileId)
            .Where(troop => troop.PlayerId == playerId)
            .Where(troop => troop.UnitInstanceId != unitInstanceId)
            .ToListAsync(cancelToken);

        return new TroopActionContext
        {
            SelectedForce = selectedTroop,
            OtherForcesOnTile = otherTroopsOnTile
        };

        // Shared projection so the selected troop and its neighbours are always shaped identically.
        // Everything the strategies read is flattened off the compiled snapshot here, so no EF entity escapes into the pure rule layer.
        IQueryable<ForceActionForce> ProjectTroops(AscendantEmpireContext dbContext)
        {
            return dbContext.UnitInstances
                .AsNoTracking()
                .Where(troop => troop.GameId == gameId)
                .Where(troop => troop.HealthyCount + troop.InjuredCount > 0) // A wiped-out troop has no actions to offer.
                .Select(troop => new ForceActionForce
                {
                    UnitInstanceId = troop.Id,
                    PlayerId = troop.PlayerId,
                    CampaignMapTileId = troop.CampaignMapTileId,
                    // The compiled snapshot always exists: a troop cannot be spawned from an uncompiled template.
                    DisplayName = troop.Name ?? troop.UnitTemplate.Compiled!.DisplayName,
                    HealthyCount = troop.HealthyCount,
                    InjuredCount = troop.InjuredCount,
                    MovementPointsRemaining = troop.MovementPointsRemaining,
                    CampaignMovementPoints = troop.UnitTemplate.Compiled!.CampaignMovementPoints,
                    Organisation = troop.UnitTemplate.Compiled!.OrganisationTag == null
                        ? null
                        : new TroopActionOrganisation
                        {
                            TagId = troop.UnitTemplate.Compiled!.OrganisationTag!.Id,
                            Code = troop.UnitTemplate.Compiled!.OrganisationTag!.Code,
                            DisplayName = troop.UnitTemplate.Compiled!.OrganisationTag!.DisplayName,
                            LeadershipModel = troop.UnitTemplate.Compiled!.OrganisationTag!.LeadershipModel,
                            UnledBaseSize = troop.UnitTemplate.Compiled!.OrganisationTag!.UnledBaseSize,
                            UnledMaxSize = troop.UnitTemplate.Compiled!.OrganisationTag!.UnledMaxSize
                        }
                });
        }
    }
}
