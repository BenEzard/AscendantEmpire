namespace AscendantEmpire.FrontendContracts.Units.SpawnUnit;

public interface ISpawnUnitDto
{
    Guid GameGuid { get; }
    Guid PlayerGuid { get; }
    int XCoord { get; }
    int YCoord { get; }
    int UnitDesignId { get; }
}
