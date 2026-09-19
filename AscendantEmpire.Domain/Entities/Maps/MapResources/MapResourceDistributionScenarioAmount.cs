namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// The starting amount of one Resource in a Distribution scenario. Keyed on the scenario and the Resource.
/// </summary>
public class MapResourceDistributionScenarioAmount
{
    public int MapResourceDistributionScenarioId { get; set; }

    public MapResourceDistributionScenario Scenario { get; set; } = null!;

    /// <summary>
    /// The Resource (a <c>resources.Resource</c>).
    /// </summary>
    public int ResourceId { get; set; }

    /// <summary>
    /// The starting amount, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public long Amount { get; set; }
}
