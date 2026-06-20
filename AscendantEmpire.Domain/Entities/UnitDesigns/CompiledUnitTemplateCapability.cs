namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A capability a compiled template has, captured as a queryable row (e.g. <c>settler</c>) rather than a
/// blob — so "which templates can found a city" is a simple <c>WHERE</c>.
/// </summary>
/// <remarks>
/// Capabilities are the union of the buff/bonus tag codes carried by the template's components.
/// </remarks>
public class CompiledUnitTemplateCapability
{
    public int Id { get; set; }

    /// <summary>FK to <see cref="CompiledUnitTemplate.UnitTemplateId"/>.</summary>
    public int CompiledUnitTemplateId { get; set; }

    /// <summary>The capability tag's stable code (e.g. <c>settler</c>).</summary>
    public required string TagCode { get; set; }

    /// <summary>Snapshot of the tag's display name at compile time.</summary>
    public required string DisplayName { get; set; }

    public CompiledUnitTemplate CompiledUnitTemplate { get; set; } = null!;
}
