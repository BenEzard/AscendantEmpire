using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.BackendContracts.TroopOrders.GetTroopOrders;

public record TroopOrder
{
    public int Id { get; init; }

    public int GameId { get; init; }

    public int TroopInstanceId { get; init; }

    public TroopOrderType OrderType { get; init; }

    public TroopOrderStatus OrderStatus { get; init; }

    public int? TargetCampaignMapTileId { get; init; }

    public int? StructureInstanceId { get; init; }

    public int? ResourceDefinitionId { get; init; }

    public int? CurrentMovementStepIndex { get; init; }

    public decimal? WorkRequired { get; init; }

    public decimal WorkCompleted { get; init; }

    public int? EffectiveFromTurnNumber { get; init; }

    public int? TradeSourceStructureInstanceId { get; init; }

    public int? TradeTargetStructureInstanceId { get; init; }

    public int? DefendTargetTroopInstanceId { get; init; }

    public int? DefendTargetStructureInstanceId { get; init; }

    public int CreatedTurnNumber { get; init; }

    public int? LastProcessedTurnNumber { get; init; }

    public int? CompletedTurnNumber { get; init; }

    public string? FailureReason { get; init; }

    public string? CancellationReason { get; init; }

    public IReadOnlyCollection<TroopOrderMovementStep> MovementSteps { get; init; } = [];

    public IReadOnlyCollection<TroopOrderParticipant> Participants { get; init; } = [];
}
