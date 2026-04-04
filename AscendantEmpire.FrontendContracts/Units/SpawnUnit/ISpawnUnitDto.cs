namespace AscendantEmpire.FrontendContracts.Units.SpawnUnit;

public interface ISpawnUnitDto
{
    int GameId { get; }
    int PlayerId { get; }
    int XCoord { get; }
    int YCoord { get; }
    int UnitDesignId { get; }
}
