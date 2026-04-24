using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.Domain.Entities.Units;

public class TroopOrder
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int TroopInstanceId { get; set; }

    public TroopOrderType OrderType { get; set; }

    public TroopOrderStatus OrderStatus { get; set; }

    /// <summary>
    /// The tile the order is ultimately targeting.
    /// For Move this is the destination tile.
    /// For Build/Gather/Encamp/Fortify/Ambush/Sentry/Explore this is usually the tile where the action occurs.
    /// </summary>
    public int? TargetCampaignMapTileId { get; set; }

    /// <summary>
    /// Optional structure type / definition to build.
    /// Only used for Build orders.
    /// </summary>
    public int? StructureInstanceId { get; set; }

    /// <summary>
    /// Optional resource type to gather.
    /// Only used for Gather orders.
    /// Replace with your actual resource FK if needed.
    /// </summary>
    public int? ResourceDefinitionId { get; set; }

    /// <summary>
    /// Cached current movement step index.
    /// Null for non-movement orders.
    /// </summary>
    public int? CurrentMovementStepIndex { get; set; }

    /// <summary>
    /// Generic progress model for multi-turn work.
    /// Examples:
    /// Build: required build work.
    /// Gather: required gather work.
    /// Explore: exploration progress target.
    /// </summary>
    public decimal? WorkRequired { get; set; }

    public decimal WorkCompleted { get; set; }

    public int? EffectiveFromTurnNumber { get; set; }

    public int? TradeSourceStructureInstanceId { get; set; }
    public int? TradeTargetStructureInstanceId { get; set; }

    public int? DefendTargetTroopInstanceId { get; set; }
    public int? DefendTargetStructureInstanceId { get; set; }

    // Debug fields
    public int CreatedTurnNumber { get; set; }
    public int? LastProcessedTurnNumber { get; set; }
    public int? CompletedTurnNumber { get; set; }
    public string? FailureReason { get; set; }
    public string? CancellationReason { get; set; }

    public virtual TroopInstance TroopInstance { get; set; } = null!;

    public virtual ICollection<TroopOrderMovementStep> MovementSteps { get; set; } = [];
    public virtual ICollection<TroopOrderParticipant> Participants { get; set; } = [];
}