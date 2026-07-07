namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A component fitted into a specific slot within a <see cref="UnitTemplate"/>.
/// </summary>
/// <remarks>
/// Each fitted component sits in one slot of the template's body, and a slot holds a single component: at
/// most one row per (template, slot). The slot referenced always belongs to the template's body component.
/// </remarks>
public class UnitTemplateComponent
{
    public int Id { get; set; }

    public int UnitTemplateId { get; set; }

    /// <summary>
    /// Which slot of the template's body is being filled.
    /// </summary>
    public int UnitComponentSlotId { get; set; }

    /// <summary>
    /// The component fitted into the slot.
    /// </summary>
    public int UnitComponentId { get; set; }

    public UnitTemplate UnitTemplate { get; set; } = null!;

    public UnitComponentSlot UnitComponentSlot { get; set; } = null!;
    public UnitComponent UnitComponent { get; set; } = null!;
}
