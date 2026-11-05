using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Delivers each of the Player's camps into the Settlement it feeds, now that the Turn's production and construction are settled.
/// </summary>
/// <remarks>
/// <para>
/// All the rules live in <see cref="IAssetBankDeliveryService"/>; this is the slot in the ordering and nothing else. A camp that is not designated, or that has been
/// cut off from its parent, is simply not among the ones it moves - and one whose parent has no room keeps whatever would not fit.
/// </para>
/// <para>
/// <b>Safe to re-run.</b> A run moves what the parent can hold and the next run sees that already delivered, so a retried end-of-Turn moves the same stock once -
/// unlike <c>Gathering</c>, which has to guard against exactly that.
/// </para>
/// </remarks>
internal class DeliverCampProduction(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IAssetBankDeliveryService deliveryService,
    ILogger<DeliverCampProduction> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Deliver Camp Production";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.DeliverCampProduction;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlayerId is null)
        {
            // Emptying every Player's camps in the Game would be a great deal worse than refusing, and the end of a Turn is always Player-scoped.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        ServiceResult<AssetDeliveryOutcome> result = await deliveryService.DeliverCampProductionAsync(db, context.GameId, context.PlayerId.Value);

        if (result.HasSucceeded == false)
        {
            const string error = "The camps' production could not be delivered to the settlements they feed.";
            logger.LogWarning("Could not deliver camp production for Player Id {PlayerId} in Game Id {GameId}: {Errors}",
                context.PlayerId.Value, context.GameId, string.Join("; ", result.Errors));

            return error;
        }

        await db.SaveChangesAsync();

        return string.Empty;
    }
}
