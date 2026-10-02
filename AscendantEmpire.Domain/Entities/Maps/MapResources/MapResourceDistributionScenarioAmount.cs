namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// The starting amount of one Asset in a Distribution scenario. Keyed on the scenario and the Asset.
/// </summary>
public class MapResourceDistributionScenarioAmount
{
    public int MapResourceDistributionScenarioId { get; set; }

    public MapResourceDistributionScenario Scenario { get; set; } = null!;

    /// <summary>
    /// The Asset (a <c>assets.Asset</c>).
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// The starting amount, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public long Amount { get; set; }
}
