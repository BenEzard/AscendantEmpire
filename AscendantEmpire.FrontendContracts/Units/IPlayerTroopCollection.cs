namespace AscendantEmpire.FrontendContracts.Units;

public interface IPlayerTroopCollection
{
    IReadOnlyCollection<IPlayerTroop> Troops { get; }
}
