namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

internal class EvaluateTroopOnHostileTerrain : IEndCampaignTurnProcess
{
    public string ItemName => "Evaluate Troop on Hostile Terrain";
    public int ExecutionOrder => 100;

    public Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Implement logic to calculate if troops remaining on hostile terrain should take damage or suffer penalties.
        return Task.FromResult(string.Empty);
    }
}