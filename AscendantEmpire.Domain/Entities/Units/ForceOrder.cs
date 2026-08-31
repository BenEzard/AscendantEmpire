using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Domain.Entities.Units;

public class ForceOrder
{
    public int Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The <see cref="UnitInstance"/> this order was issued to, or null when it was issued to a <see cref="Units.Command"/> instead.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="UnitInstanceId"/> and <see cref="CommandId"/> is populated, enforced by the database check constraint
    /// <c>CK_ForceOrders_UnitOrCommandXor</c>. An order targets a <b>Force</b>, and a Force is either a Unit or a Command.
    /// </remarks>
    public int? UnitInstanceId { get; set; }

    /// <summary>
    /// The <see cref="Units.Command"/> this order was issued to, or null when it was issued to a single <see cref="UnitInstance"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The nullable pair was chosen over fanning a Command's order out to its member Units. "A Command moves as one" is a rule, and one order row
    /// against the Command <i>states</i> it; fanning out would make it something that has to be kept true by hand across splits, casualties and
    /// partial movement.
    /// </para>
    /// </remarks>
    public int? CommandId { get; set; }

    public ForceOrderType OrderType { get; set; }

    public ForceOrderStatus OrderStatus { get; set; }

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

    public int? DefendTargetUnitInstanceId { get; set; }
    public int? DefendTargetStructureInstanceId { get; set; }

    // Debug fields
    public int CreatedTurnNumber { get; set; }
    public int? LastProcessedTurnNumber { get; set; }
    public int? CompletedTurnNumber { get; set; }
    public string? FailureReason { get; set; }
    public string? CancellationReason { get; set; }

    public virtual UnitInstance? UnitInstance { get; set; }

    public virtual Command? Command { get; set; }

    public virtual ICollection<ForceOrderMovementStep> MovementSteps { get; set; } = [];
    public virtual ICollection<ForceOrderParticipant> Participants { get; set; } = [];
}