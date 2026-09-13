namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

internal class Fortifying : IEndCampaignTurnProcess
{
    public string ItemName => "Fortifying";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.Fortifying;

    public Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Implement logic to alert sentries about the start of the turn, such as updating their status or sending notifications.
        //  Check the units on Sentry, and see if there's any units in there field of vision
        //  Note this would also include other structures etc, so not quite sure how the logic would look
        //
        // TODO: Fortifications are built on the battle map, so when a Force's fortifications are built the campaign tile's battle map must exist. Generate it (or reuse it) through
        //  GenerateBattleMapRequest, as IBattleEngagementService does, and initialise the builder's view of it through IPlayerBattleMapService.InitialiseAsync.
        return Task.FromResult(string.Empty);
    }
}
