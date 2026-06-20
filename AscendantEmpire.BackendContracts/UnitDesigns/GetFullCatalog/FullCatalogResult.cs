using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;

/// <summary>
/// The entire unit-design catalog, unfiltered — every body, component, tag and template. The read
/// counterpart of the bulk-upsert write side, for a developer design tool.
/// </summary>
public record FullCatalogResult
{
    public required IReadOnlyCollection<DesignBody> Bodies { get; init; }
    public required IReadOnlyCollection<DesignComponent> Components { get; init; }
    public required IReadOnlyCollection<CatalogTag> Tags { get; init; }
    public required IReadOnlyCollection<DesignTemplate> Templates { get; init; }
}
