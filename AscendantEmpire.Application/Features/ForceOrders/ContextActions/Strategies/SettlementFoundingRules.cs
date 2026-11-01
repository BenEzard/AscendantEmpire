using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// The conditions every settlement-founding action shares: where a Force may settle, and whether it is in a fit state to do so.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreateVillageStrategy"/> and <see cref="EstablishCampStrategy"/> differ only in the capability that unlocks them and in how many
/// troops they need. Everything else is the same question asked twice, so it is answered here once — a pure static, which keeps both strategies
/// free of the database exactly as <see cref="IForceActionStrategy"/> requires.
/// </para>
/// </remarks>
internal static class SettlementFoundingRules
{
    /// <summary>
    /// Every reason the given Force cannot found a settlement of this kind right now, in a fixed order.
    /// </summary>
    /// <param name="force">The Force being judged.</param>
    /// <param name="settlementNoun">What is being founded, lower case, for the message text — "village", "camp".</param>
    /// <param name="minimumTroops">The smallest Force that may found one.</param>
    /// <returns>An empty list when nothing is blocking it, which the caller turns into an available action.</returns>
    /// <remarks>
    /// Every failing rule is collected rather than only the first, so a Force that is both on water and too small is told both at once instead of
    /// fixing one problem to discover the next.
    /// </remarks>
    public static List<string> CollectBlockingReasons(ForceActionForce force, string settlementNoun, int minimumTroops)
    {
        ArgumentNullException.ThrowIfNull(force);

        List<string> reasons = [];

        if (force.TileIsWater)
        {
            reasons.Add($"{force.DisplayName} is on water, and a {settlementNoun} must be founded on land.");
        }

        if (CampaignTileTerrain.GeographyIsImpassable(force.TileGeographyIsTraversable))
        {
            reasons.Add($"{force.DisplayName} is in {force.TileGeographyName} terrain, which cannot be settled.");
        }

        // Any movement left will do; this is not one of the actions that demands a whole untouched turn. Note the value can legitimately be
        // negative — a Force holding any movement at all may enter a tile and pays its whole cost — so a Force in movement debt is refused here, as it should be.
        if (force.CampaignMovementRemaining <= 0)
        {
            reasons.Add($"{force.DisplayName} has no campaign movement left this turn.");
        }

        if (force.TotalStrength < minimumTroops)
        {
            reasons.Add($"{force.DisplayName} has {force.TotalStrength} troops, and a {settlementNoun} needs at least {minimumTroops}.");
        }

        return reasons;
    }
}
