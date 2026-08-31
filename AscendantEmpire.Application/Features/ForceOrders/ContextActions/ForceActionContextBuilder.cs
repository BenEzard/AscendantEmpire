using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <inheritdoc cref="IForceActionContextBuilder"/>
internal sealed class ForceActionContextBuilder(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IForceActionContextBuilder
{
    public async Task<ForceActionContext?> BuildAsync(int gameId, int playerId, int unitInstanceId, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // The selected Force is fetched on its own rather than as part of the tile query, so that "no such Force" and "the Force is there but
        // it isn't yours" can be told apart from "the tile is empty" by the caller.
        ForceActionForce? selectedForce = await ProjectForces(context)
            .Where(force => force.UnitInstanceId == unitInstanceId)
            .FirstOrDefaultAsync(cancelToken);

        if (selectedForce is null || selectedForce.PlayerId != playerId)
        {
            return null;
        }

        // Only the player's own Forces are gathered. Including enemy Forces would need the vision rules applied first, or the toolbar would
        // quietly confirm what is standing on a tile the player cannot see — see the design doc's open questions.
        List<ForceActionForce> otherForcesOnTile = await ProjectForces(context)
            .Where(force => force.CampaignMapTileId == selectedForce.CampaignMapTileId)
            .Where(force => force.PlayerId == playerId)
            .Where(force => force.UnitInstanceId != unitInstanceId)
            .ToListAsync(cancelToken);

        return new ForceActionContext
        {
            SelectedForce = selectedForce,
            OtherForcesOnTile = otherForcesOnTile
        };

        // Shared projection so the selected Force and its neighbours are always shaped identically.
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
