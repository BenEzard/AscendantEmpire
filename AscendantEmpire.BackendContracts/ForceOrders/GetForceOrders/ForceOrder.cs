using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.BackendContracts.ForceOrders.GetForceOrders;

public record ForceOrder
{
    public int Id { get; init; }

    public int GameId { get; init; }

    /// <summary>
    /// The Unit this order was issued to, or null when it was issued to a Command.
    /// </summary>
    /// <remarks>Exactly one of <see cref="UnitInstanceId"/> and <see cref="CommandId"/> is populated: an order targets a Force.</remarks>
    public int? UnitInstanceId { get; init; }

    /// <summary>
    /// The Command this order was issued to, or null when it was issued to a single Unit.
    /// </summary>
    public int? CommandId { get; init; }

    public int OrderSequence { get; init; }

    public ForceOrderType OrderType { get; init; }

    public ForceOrderStatus OrderStatus { get; init; }

    public int? TargetCampaignMapTileId { get; init; }

    public int? StructureInstanceId { get; init; }

    public int? ResourceDefinitionId { get; init; }

    public int? CurrentMovementStepIndex { get; init; }

    public decimal? WorkRequired { get; init; }

    public decimal WorkCompleted { get; init; }

    public int? EffectiveFromTurnNumber { get; init; }

    public int? TradeSourceStructureInstanceId { get; init; }

    public int? TradeTargetStructureInstanceId { get; init; }

    public int? DefendTargetUnitInstanceId { get; init; }

    public int? DefendTargetStructureInstanceId { get; init; }

    public int CreatedTurnNumber { get; init; }

    public int? LastProcessedTurnNumber { get; init; }

    public int? CompletedTurnNumber { get; init; }

    public string? FailureReason { get; init; }

    public string? CancellationReason { get; init; }

    public IReadOnlyCollection<ForceOrderMovementStep> MovementSteps { get; init; } = [];

    public IReadOnlyCollection<ForceOrderParticipant> Participants { get; init; } = [];
}
