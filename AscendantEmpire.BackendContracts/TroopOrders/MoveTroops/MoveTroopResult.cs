namespace AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;

public record MoveTroopResult
{
    public int TroopOrderId { get; init; }

    public int UnitInstanceId { get; init; }

    public int TargetCampaignMapTileId { get; init; }

    public int PathStepCount { get; init; }

    public int EstimatedTurns { get; init; }
}
