using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Gets any troops belonging to the Player that still have movement points remaining on the Campaign <ap.
/// </summary>
internal sealed class CampaignRemainingMovementCheck(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IPossibleActionCheck
{
    public string CheckName => "Campaign Troop Remaining Movement";
    public int ExecutionOrder => (int)EndTurnPossibleActionOrder.TroopsWithRemainingCampaignMovements;

    public async Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext actionContext,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find troops with remaining movement on the Campaign Map
        var idleTroops = await db.UnitInstances
            .Include(t => t.CampaignMap)
            .Where(t => t.GameId == actionContext.GameId)
            .Where(t => t.PlayerId == actionContext.PlayerId)
            .Where(t => t.BattleMapTileId == null)
            .Where(t => t.HealthyCount + t.InjuredCount > 0)
            .Where(t => t.MovementPointsRemaining >= 0) // First filter, not all of these will necessarily be included 
            .Select(t => new 
            { 
                t.Id, 
                t.Name,
                Coord = new Coord(t.CampaignMap.XCoord, t.CampaignMap.YCoord),
            })
            .ToListAsync(cancellationToken);

        // TODO: Analyse the idle troops and surrounding radius to see if any of them can actually move anywhere.

        return [.. idleTroops.Select(t =>
            new PossibleAction {
                Category = InformationCategory.Military,
                Description = $"{(string.IsNullOrWhiteSpace(t.Name) ? "Unnamed" : t.Name)} has movement remaining.",
                Id = t.Id,
                Coord = t.Coord,
                RecordTypeId = (int)RecordType.Unit // TODO: Determine whether this is a Unit or a Command
            })];
    }
}