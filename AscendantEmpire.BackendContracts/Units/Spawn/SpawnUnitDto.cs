using AscendantEmpire.FrontendContracts.Units.SpawnUnit;

namespace AscendantEmpire.BackendContracts.Units.Spawn;

public record class SpawnUnitDto : ISpawnUnitDto
{
    public Guid GameGuid { get; init; }

    public Guid PlayerGuid { get; init; }

    public int XCoord { get; init; }
    public int YCoord { get; init; }

    public int UnitDesignId { get; init; }
}
