using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Domain.Entities.Units;

public class ForceOrderParticipant
{
    public int Id { get; set; }

    public int ForceOrderId { get; set; }

    public int UnitInstanceId { get; set; }

    /// <summary>
    /// Primary = the Force the order is attached to.
    /// Secondary = another Force involved in the order.
    /// Result = optionally the Force created by the split/combine operation.
    /// </summary>
    public ForceOrderParticipantRole Role { get; set; }

    public virtual ForceOrder ForceOrder { get; set; } = null!;

    public virtual UnitInstance UnitInstance { get; set; } = null!;
}