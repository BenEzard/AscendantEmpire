using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;

internal class HealTroops(IDbContextFactory<AscendantEmpireContext> dbFactory) : IEndCampaignRoundProcess
{
    public string ItemName { get => "Heal Troops"; }
    public int ExecutionOrder => 100; // After all the checks and possible actions, but before the final end turn processing.


    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // Get the wounded troops


        // TODO: Code the Heal Troops logic.
        //      Consider things like entourage including medics, doctors, suregeries etc
        //      Consider if they are on friendly territory, in a city, in a camp, in the wild etc
        //      TroopOrderType = Heal (3)


        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();
        return string.Empty;
    }
}
