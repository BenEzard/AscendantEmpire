namespace AscendantEmpire.FrontendContracts.Units.SpawnUnit;

public interface ISpawnUnitDto
{
    int GameId { get; }
    int PlayerId { get; }
    int XCoord { get; }
    int YCoord { get; }

    /// <summary>
    /// The <c>UnitTemplate</c> to spawn from (the unit's stats come from its compiled snapshot).
    /// </summary>
    int UnitTemplateId { get; }
    /// <summary>
    /// Initial number of troops
    /// </summary>
    int InitialStrength { get; }
    int HealthyCount { get; }
    int InjuredCount { get; }
}
