namespace AscendantEmpire.Domain.Enums.MapResources;

/// <summary>
/// Which of a MapResourceType's values a <c>PlayerMapResourceModifier</c> changes for that Player.
/// </summary>
/// <remarks>
/// <para>
/// The first three are the habitat values the spec lists under "Player modifiers", and apply on tiles the Player has claimed. The last two come from "Discovery": tech lowers
/// the fuzzy factor a Player judges quantities with, and researching a type makes that Player more likely to discover it. See <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// All but <see cref="MaximumQuantity"/> are fractions, so for those a modifier's flat part is in percentage points (a 10% value with a +5 point modifier becomes 15%), while
/// its percentage part is a share of the base value, as for every other modifier.
/// </para>
/// </remarks>
public enum MapResourceModifierTarget
{
    /// <summary>
    /// The ceiling a renewable instance grows to (for a Distribution, the ceiling on its total).
    /// </summary>
    MaximumQuantity = 1,

    /// <summary>
    /// The share of its current quantity a renewable instance grows each Round.
    /// </summary>
    GrowthPercentage = 2,

    /// <summary>
    /// The chance each Round that an instance at its ceiling spreads.
    /// </summary>
    SpreadLikelihood = 3,

    /// <summary>
    /// How far off the quantity a Player is shown can be. A lower factor means a closer estimate.
    /// </summary>
    EstimationFuzzyFactor = 4,

    /// <summary>
    /// The chance of discovering an instance when a unit enters or rests on its tile.
    /// </summary>
    DiscoveryLikelihood = 5
}
