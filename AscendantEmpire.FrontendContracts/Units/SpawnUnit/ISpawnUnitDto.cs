namespace AscendantEmpire.FrontendContracts.Units.SpawnUnit;

public interface ISpawnUnitDto
{
    int GameId { get; }
    int PlayerId { get; }
    int XCoord { get; }
    int YCoord { get; }
    int UnitDesignId { get; }
    /// <summary>
    /// Initial number of troops
    /// </summary>
    int InitialStrength { get; }
    int HealthyCount { get; }
    int InjuredCount { get; }
}
