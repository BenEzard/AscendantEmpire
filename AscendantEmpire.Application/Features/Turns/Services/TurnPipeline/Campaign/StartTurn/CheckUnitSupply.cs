namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

internal class CheckUnitSupply : IStartCampaignTurnProcess
{
    public string ItemName => "Check Unit Supply";

    public int ExecutionOrder => (int)StartCampaignTurnProcessOrder.CheckUnitSupply;

    public Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Implement logic to alert check Unit supply
        return Task.FromResult(string.Empty);
    }
}
