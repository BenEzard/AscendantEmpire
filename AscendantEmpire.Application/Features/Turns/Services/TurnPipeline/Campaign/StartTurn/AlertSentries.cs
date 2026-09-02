namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

internal class AlertSentries : IStartCampaignTurnProcess
{
    public string ItemName => "Alert Sentries";

    public int ExecutionOrder => (int)StartCampaignTurnProcessOrder.AlertSentries;

    public Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Implement logic to alert sentries about the start of the turn, such as updating their status or sending notifications.
        //  Check the units on Sentry, and see if there's any units in there field of vision
        //  Note this would also include other structures etc, so not quite sure how the logic would look
        return Task.FromResult(string.Empty);
    }
}
