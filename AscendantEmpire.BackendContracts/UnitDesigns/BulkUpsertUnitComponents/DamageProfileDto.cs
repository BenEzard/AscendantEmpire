namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Damage split across the five damage types, used for weapon attack and armour resistance.
/// </summary>
/// <remarks>
/// Mirrors the <c>DamageProfile</c> EF complex type. Each value is a raw fixed-point integer using
/// the Scale 1,000 convention (1,000 raw = 1.0).
/// </remarks>
public record DamageProfileDto
{
    public long Slash { get; init; }
    public long Pierce { get; init; }
    public long Crush { get; init; }
    public long Burn { get; init; }
    public long Poison { get; init; }
}
