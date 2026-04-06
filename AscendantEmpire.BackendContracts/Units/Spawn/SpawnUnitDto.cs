using AscendantEmpire.FrontendContracts.Units.SpawnUnit;

namespace AscendantEmpire.BackendContracts.Units.Spawn;

public record class SpawnUnitDto : ISpawnUnitDto
{
    public int GameId { get; init; }

    public int PlayerId { get; init; }

    public int XCoord { get; init; }
    public int YCoord { get; init; }

    public int UnitDesignId { get; init; }
    public int InitialStrength { get; init; }
    public int HealthyCount { get; init; }
    public int InjuredCount { get; init; }
}
