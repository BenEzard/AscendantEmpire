using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.Domain.Entities.Units;

public class TroopOrderParticipant
{
    public int Id { get; set; }

    public int TroopOrderId { get; set; }

    public int UnitInstanceId { get; set; }

    /// <summary>
    /// Primary = the troop the order is attached to.
    /// Secondary = another troop involved in the order.
    /// Result = optionally the troop created by the split/combine operation.
    /// </summary>
    public TroopOrderParticipantRole Role { get; set; }

    public virtual TroopOrder TroopOrder { get; set; } = null!;

    public virtual UnitInstance UnitInstance { get; set; } = null!;
}