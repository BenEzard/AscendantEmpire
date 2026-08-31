using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Domain.Entities.Units;

public class ForceOrderParticipant
{
    public int Id { get; set; }

    public int ForceOrderId { get; set; }

    public int UnitInstanceId { get; set; }

    /// <summary>
    /// Primary = the troop the order is attached to.
    /// Secondary = another troop involved in the order.
    /// Result = optionally the troop created by the split/combine operation.
    /// </summary>
    public ForceOrderParticipantRole Role { get; set; }

    public virtual ForceOrder ForceOrder { get; set; } = null!;

    public virtual UnitInstance UnitInstance { get; set; } = null!;
}