namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// Damage split across the five types (fixed-point, Scale 1,000), used for weapon attack and armour
/// resistance display.
/// </summary>
public record DesignDamageProfile
{
    public required long Slash { get; init; }
    public required long Pierce { get; init; }
    public required long Crush { get; init; }
    public required long Burn { get; init; }
    public required long Poison { get; init; }
}
