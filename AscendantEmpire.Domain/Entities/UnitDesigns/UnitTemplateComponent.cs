namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A component fitted into a specific slot within a <see cref="UnitTemplate"/>.
/// </summary>
/// <remarks>
/// <para>The fitted components form a tree. A row with a null <see cref="ParentTemplateComponentId"/> is
/// fitted directly into a slot of the template's body (the root); a row with a parent set is fitted into
/// a slot of that parent fitted-component — this is how slot-bearing components nest.</para>
/// <para>A slot holds a single component <i>per parent instance</i>: at most one row per
/// (template, parent, slot). The slot referenced must belong to the body component when the parent is
/// null, otherwise to the parent's <see cref="UnitComponent"/>.</para>
/// </remarks>
public class UnitTemplateComponent
{
    public int Id { get; set; }

    public int UnitTemplateId { get; set; }

    /// <summary>
    /// The parent fitted-component this one nests inside. Null = fitted directly into a body slot (root).
    /// </summary>
    public int? ParentTemplateComponentId { get; set; }

    /// <summary>
    /// Which slot is being filled — of the template's body when <see cref="ParentTemplateComponentId"/>
    /// is null, otherwise of the parent fitted-component's <see cref="UnitComponent"/>.
    /// </summary>
    public int UnitComponentSlotId { get; set; }

    /// <summary>
    /// The component fitted into the slot.
    /// </summary>
    public int UnitComponentId { get; set; }

    public UnitTemplate UnitTemplate { get; set; } = null!;

    /// <summary>
    /// The parent fitted-component; null for components fitted directly into a body slot.
    /// </summary>
    public UnitTemplateComponent? ParentTemplateComponent { get; set; }

    /// <summary>
    /// The components fitted into this fitted-component's own slots (0 or more).
    /// </summary>
    public ICollection<UnitTemplateComponent> Children { get; set; } = [];

    public UnitComponentSlot UnitComponentSlot { get; set; } = null!;
    public UnitComponent UnitComponent { get; set; } = null!;
}
