using AscendantEmpire.FrontendContracts.Units.SpawnUnit;

namespace AscendantEmpire.BackendContracts.Units.Spawn;

public record class SpawnUnitDto : ISpawnUnitDto
{
    public int GameId { get; init; }

    public int PlayerId { get; init; }

    public int XCoord { get; init; }
    public int YCoord { get; init; }

    public int UnitDesignId { get; init; }
}
