using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// One set of starting amounts a Distribution instance can be created from, e.g. Forest Wood scenario 2: generic 5,000 and ebony 300.
/// </summary>
/// <remarks>
/// A template only. An instance copies the amounts into its own <c>game.MapResourceInstanceAmount</c> rows, so felling oak on one tile never changes another tile using the
/// same scenario. Which scenario a tile gets is picked at game setup, weighted by its Biome (<see cref="BiomeWeights"/>), unless the map editor chose one.
/// </remarks>
public class MapResourceDistributionScenario
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    public int MapResourceTypeId { get; set; }

    public MapResourceType MapResourceType { get; set; } = null!;

    /// <summary>
    /// The scenario's number within its type, from 1. Unique per type.
    /// </summary>
    public int ScenarioNumber { get; set; }

    /// <summary>
    /// An optional name for the map editor, e.g. "Oak-rich".
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// The starting amount of each Resource.
    /// </summary>
    public ICollection<MapResourceDistributionScenarioAmount> Amounts { get; set; } = [];

    /// <summary>
    /// How likely the scenario is to be picked in each Biome. A Biome with no weight never gets it at random.
    /// </summary>
    public ICollection<MapResourceDistributionScenarioBiomeWeight> BiomeWeights { get; set; } = [];
}
