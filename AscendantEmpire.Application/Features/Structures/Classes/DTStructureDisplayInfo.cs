using AscendantEmpire.FrontendContracts.Structures.GetStructureDisplayInfo;

namespace AscendantEmpire.Application.Features.Structures.Classes;

/// <inheritdoc cref="IStructureDisplayInfo" />
public class DTStructureDisplayInfo : IStructureDisplayInfo
{
    public required int StructureId { get; set; }
    public required string Name { get; set; }
    public required string MapImagePath { get; set; }
}

/// <inheritdoc cref="IStructureDisplayInfoCollection" />
public class DTStructureDisplayInfoCollection : IStructureDisplayInfoCollection
{
    public required IReadOnlyCollection<IStructureDisplayInfo> StructureDisplayInfo { get; set; }
}
