using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Gets any Forces belonging to the Player that still have movement points remaining on the Campaign Map.
/// </summary>
internal sealed class CampaignRemainingMovementCheck(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IPossibleActionCheck
{
    public string CheckName => "Campaign Force Remaining Movement";
    public int ExecutionOrder => (int)EndTurnPossibleActionOrder.ForcesWithRemainingCampaignMovements;

    public async Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext actionContext,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find Forces with remaining movement on the Campaign Map
        var idleForces = await db.UnitInstances
            .Include(t => t.CampaignMap)
            .Where(t => t.GameId == actionContext.GameId)
            .Where(t => t.PlayerId == actionContext.PlayerId)
            .Where(t => t.BattleMapTileId == null)
            .Where(t => t.HealthyCount + t.InjuredCount > 0)
            // Strictly positive, and no threshold beyond that. Campaign movement costs vary by terrain and a Force needs only half a tile's cost to
            // enter it, so any movement at all might be enough to go somewhere — while a Force sitting on zero, or in debt after crossing expensive
            // ground, certainly cannot. This is still a first filter: it says the Force *has* movement, not that anywhere is open to it.
            .Where(t => t.CampaignMovementRemaining > 0)
            .Select(t => new
            {
                t.Id,
                t.Name,
                Coord = new Coord(t.CampaignMap.XCoord, t.CampaignMap.YCoord),
            })
            .ToListAsync(cancellationToken);

        // TODO: Analyse the idle Forces and surrounding radius to see if any of them can actually move anywhere.

        return [.. idleForces.Select(t =>
            new PossibleAction {
                Category = InformationCategory.Military,
                Description = $"{(string.IsNullOrWhiteSpace(t.Name) ? "Unnamed" : t.Name)} has movement remaining.",
                Id = t.Id,
                Coord = t.Coord,
                RecordTypeId = (int)RecordType.Unit // TODO: Determine whether this is a Unit or a Command
            })];
    }
}
