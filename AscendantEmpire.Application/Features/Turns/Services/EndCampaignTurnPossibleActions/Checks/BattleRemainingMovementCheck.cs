using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Gets any troops belonging to the Player that still have movement points remaining in active Battles.
/// </summary>
internal sealed class BattleRemainingMovementCheck(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IPossibleActionCheck
{
    public string CheckName => "Battle Troop Remaining Movement";
    public int ExecutionOrder => (int)EndTurnPossibleActionOrder.TroopsWithRemainingBattleMovements;

    public async Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext actionContext,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var idleBattleTroops = await db.UnitInstances
            .Include(t => t.CampaignMap)
            .Where(t => t.GameId == actionContext.GameId)
            .Where(t => t.PlayerId == actionContext.PlayerId)
            .Where(t => t.BattleMapTileId != null)
            .Where(t => t.HealthyCount + t.InjuredCount > 0)
            // Movement on a Battle Map always costs 1 MP, which is one whole Scale 1,000 unit of the raw fixed-point value stored on the troop.
            .Where(t => t.MovementPointsRemaining >= ScaledAmount.Scale)
            .Select(t => new 
            { 
                t.Id, 
                t.Name,
                Coord = new Coord(t.CampaignMap.XCoord, t.CampaignMap.YCoord),
            })
            .ToListAsync(cancellationToken);

        return [.. idleBattleTroops.Select(t =>
            new PossibleAction 
            {
                Category = InformationCategory.Military,
                Description = $"{(string.IsNullOrWhiteSpace(t.Name) ? "Unnamed" : t.Name)} has movement remaining.",
                Id = t.Id,
                Coord = t.Coord,
                RecordTypeId = (int)RecordType.Unit // TODO: Determine whether this is a Unit or a Command
            })];
    }
}