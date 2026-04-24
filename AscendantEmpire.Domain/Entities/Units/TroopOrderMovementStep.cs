using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Units;

public class TroopOrderMovementStep
{
    public int Id { get; set; }

    public int TroopOrderId { get; set; }

    /// <summary>
    /// Zero-based order of the path step.
    /// </summary>
    public int StepIndex { get; set; }

    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// Cost to enter this tile for this planned route.
    /// Storing this lets you detect route invalidation when costs change.
    /// </summary>
    public decimal MovementCost { get; set; }

    public bool IsCompleted { get; set; }

    public int? CompletedTurnNumber { get; set; }

    public virtual TroopOrder TroopOrder { get; set; } = null!;
}
