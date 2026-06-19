namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// The category a <c>UnitComponent</c> belongs to.
/// </summary>
/// <remarks>
/// <para>Order follows the design doc. Stored in the database as an <c>int</c>.</para>
/// <para>Every Unit Template has exactly one <see cref="Body"/> component; all other
/// component types are fitted into the body's Template Slots.</para>
/// </remarks>
public enum ComponentType
{
    Body,
    Weapon,
    Engine,
    Armour,
    Equipment,
    Radar,
    System,
    Fitting,
    Training,
    Special,
}
