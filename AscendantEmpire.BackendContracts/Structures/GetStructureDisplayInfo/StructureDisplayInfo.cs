using AscendantEmpire.FrontendContracts.Structures.GetStructureDisplayInfo;

namespace AscendantEmpire.BackendContracts.Structures.GetStructureDisplayInfo;

/// <inheritdoc cref="IStructureDisplayInfo" />
public record StructureDisplayInfo : IStructureDisplayInfo
{
    public int StructureId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string MapImagePath { get; init; } = string.Empty;
}
