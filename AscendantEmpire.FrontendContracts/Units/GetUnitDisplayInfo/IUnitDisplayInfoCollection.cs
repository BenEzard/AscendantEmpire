namespace AscendantEmpire.FrontendContracts.Units.GetUnitDisplayInfo;

public interface IUnitDisplayInfoCollection
{
    IReadOnlyCollection<IUnitDisplayInfo> UnitDisplayInfo { get; }
}
