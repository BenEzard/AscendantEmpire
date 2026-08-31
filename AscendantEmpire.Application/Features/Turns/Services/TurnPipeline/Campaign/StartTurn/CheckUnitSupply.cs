namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

internal class CheckUnitSupply : IStartCampaignTurnProcess
{
    public string ItemName => "Check Troop Supply";

    public int ExecutionOrder => 100;

    public Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Implement logic to alert check troop supply
        return Task.FromResult(string.Empty);
    }
}
