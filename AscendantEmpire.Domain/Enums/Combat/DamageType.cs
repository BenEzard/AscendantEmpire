namespace AscendantEmpire.Domain.Enums.Combat;

/// <summary>
/// The kinds of damage a weapon can deal and armour can mitigate.
/// </summary>
/// <remarks>
/// Mirrors the members of <see cref="Entities.UnitDesigns.DamageProfile"/>. Used for iterating
/// damage types in combat and UI code. Stored as an <c>int</c> if it is ever persisted to a column.
/// </remarks>
public enum DamageType
{
    Slash,
    Pierce,
    Crush,
    Burn,
    Poison,
}
