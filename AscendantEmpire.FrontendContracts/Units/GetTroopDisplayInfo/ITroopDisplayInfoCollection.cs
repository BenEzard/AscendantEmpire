namespace AscendantEmpire.FrontendContracts.Units.GetTroopDisplayInfo;

public interface ITroopDisplayInfoCollection
{
    IReadOnlyCollection<ITroopDisplayInfo> TroopDisplayInfo { get; }
}
