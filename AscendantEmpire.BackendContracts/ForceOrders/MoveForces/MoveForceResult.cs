namespace AscendantEmpire.BackendContracts.ForceOrders.MoveForces;

public record MoveForceResult
{
    public int ForceOrderId { get; init; }

    public int UnitInstanceId { get; init; }

    public int TargetCampaignMapTileId { get; init; }

    public int PathStepCount { get; init; }

    public int EstimatedTurns { get; init; }
}
