namespace AscendantEmpire.FrontendContracts.Structures.GetStructureDisplayInfo;

/// <summary>
/// Display information for every kind of Structure that can stand on the campaign map.
/// </summary>
public interface IStructureDisplayInfoCollection
{
    IReadOnlyCollection<IStructureDisplayInfo> StructureDisplayInfo { get; }
}
