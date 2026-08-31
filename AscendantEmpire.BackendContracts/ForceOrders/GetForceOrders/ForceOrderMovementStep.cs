namespace AscendantEmpire.BackendContracts.ForceOrders.GetForceOrders;

public record ForceOrderMovementStep
{
    public int Id { get; init; }

    public int StepIndex { get; init; }

    public int CampaignMapTileId { get; init; }

    public decimal MovementCost { get; init; }

    public bool IsCompleted { get; init; }

    public int? CompletedTurnNumber { get; init; }
}
