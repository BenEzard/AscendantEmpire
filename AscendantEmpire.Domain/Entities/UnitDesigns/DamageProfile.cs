namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// The amount of damage dealt or mitigated, split across the five damage types.
/// </summary>
/// <remarks>
/// <para>
/// Used as an EF Core complex type, so it has no identity of its own and its columns live
/// directly on the owning table. It is shared by weapons (as their attack output) and armour
/// (as flat damage reduction).
/// </para>
/// <para>
/// Each value is a raw fixed-point integer using the same Scale 1,000 convention as
/// <c>ScaledAmount</c> (1,000 raw = 1.0). Entities store the raw <see langword="long"/>; the
/// combat layer wraps these via <c>ScaledAmount.FromRaw(...)</c> to do the arithmetic.
/// </para>
/// </remarks>
public class DamageProfile
{
    public long Slash { get; set; }

    public long Pierce { get; set; }

    public long Crush { get; set; }

    public long Burn { get; set; }

    public long Poison { get; set; }
}
