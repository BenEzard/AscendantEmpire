using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// How likely a Distribution scenario is to be picked for a tile of one Biome, relative to the type's other scenarios there. Keyed on the scenario and the Biome.
/// </summary>
/// <remarks>
/// A weight, not a probability: in a Biome where scenario 1 weighs 3 and scenario 2 weighs 1, scenario 1 is picked three times as often.
/// </remarks>
public class MapResourceDistributionScenarioBiomeWeight
{
    public int MapResourceDistributionScenarioId { get; set; }

    public MapResourceDistributionScenario Scenario { get; set; } = null!;

    public BiomeType BiomeTypeId { get; set; }

    /// <summary>
    /// The scenario's weight in the Biome. Always above 0.
    /// </summary>
    public int Weight { get; set; }
}
