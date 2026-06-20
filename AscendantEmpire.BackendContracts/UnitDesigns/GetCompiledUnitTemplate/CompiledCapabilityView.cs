namespace AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;

/// <summary>
/// A capability the compiled design has (e.g. <c>settler</c>), from a tag carried by its components.
/// </summary>
public record CompiledCapabilityView
{
    public required string TagCode { get; init; }
    public required string DisplayName { get; init; }
}
