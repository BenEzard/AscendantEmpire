namespace AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;

/// <summary>
/// Aggregated damage split across the five types (fixed-point, Scale 1,000).
/// </summary>
public record CompiledDamageProfile
{
    public required long Slash { get; init; }
    public required long Pierce { get; init; }
    public required long Crush { get; init; }
    public required long Burn { get; init; }
    public required long Poison { get; init; }
}
