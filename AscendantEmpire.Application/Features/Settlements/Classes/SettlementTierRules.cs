using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.Application.Features.Settlements.Classes;

/// <summary>
/// What each foundable settlement tier corresponds to elsewhere: the toolbar action that offers it, and the Structure catalogue code that stands for it on a tile.
/// </summary>
/// <remarks>
/// <para>
/// Three facts about one tier live in three different places — the action type in <c>ForceActionType</c>, the rules in the strategy behind it, and the artwork in the
/// Structure catalogue — and this is the single join between them. Without it the settle handler would carry a <c>switch</c> over tiers for each, and the three could
/// disagree about what a village is.
/// </para>
/// <para>
/// The <b>capability tag</b> is deliberately not here. It belongs to the strategy, which already owns it and is already re-run at execution time through
/// <c>IForceContextActionService.EvaluateActionAsync</c>; restating it would be a second copy of a rule that can then drift.
/// </para>
/// <para>
/// The <b>troop minimum</b> used to be kept out for the same reason, and is here now because it stopped being only a refusal reason. Founding a camp with a Force at
/// least twice the minimum now <i>deducts</i> that many troops and leaves the Force standing, so the settle handler needs the number itself rather than a verdict about
/// it. One home is what keeps the amount deducted and the amount demanded the same number.
/// </para>
/// <para>
/// Only <see cref="SettlementTier.Camp"/> and <see cref="SettlementTier.Village"/> appear. The higher tiers are reached by growth, which is not built, and asking for one
/// of them is a refusal rather than a missing case.
/// </para>
/// </remarks>
internal static class SettlementTierRules
{
    /// <summary>The tiers a Force can found directly, and what each one maps to.</summary>
    private static readonly Dictionary<SettlementTier, SettlementTierDefinition> _DEFINITIONS = new()
    {
        [SettlementTier.Camp] = new SettlementTierDefinition
        {
            ActionType = ForceActionType.EstablishCamp,
            StructureCode = StructureSeedData.CAMP_CODE,
            IsNamed = false,
            MinimumTroops = 25,
            SplitsFromFounder = true
        },
        [SettlementTier.Village] = new SettlementTierDefinition
        {
            ActionType = ForceActionType.CreateVillage,
            StructureCode = StructureSeedData.VILLAGE_CODE,
            IsNamed = true,
            MinimumTroops = 100,
            SplitsFromFounder = false
        },
    };

    /// <summary>
    /// How many tiles out from its own tile a Settlement of each tier owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A property of the tier rather than of the Structure catalogue: the radius is what a Settlement <i>is</i>, and the Structure standing on the tile is only how it is
    /// drawn. A camp owns the ground it stands on; a village owns a ring around it.
    /// </para>
    /// <para>
    /// Deliberately a dictionary of its own rather than a field on <see cref="SettlementTierDefinition"/>, because that one holds only the <i>foundable</i> tiers while
    /// every tier has a territory - including the grown ones, which have no authored radius yet and are absent rather than guessed.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<SettlementTier, int> _TERRITORY_RADIUS_BY_TIER = new()
    {
        [SettlementTier.Camp] = 0,
        [SettlementTier.Village] = 1,
    };

    /// <summary>
    /// The definition for a tier, or <see langword="null"/> when the tier cannot be founded directly.
    /// </summary>
    public static SettlementTierDefinition? For(SettlementTier tier)
    {
        return _DEFINITIONS.GetValueOrDefault(tier);
    }

    /// <summary>
    /// How far out a Settlement of this tier owns ground.
    /// </summary>
    /// <remarks>
    /// A <c>Try</c> rather than a throw, and rather than answering 0. A tier with no authored radius is unreachable today - growth is not built - but it would reach this
    /// from inside the end-of-Turn pipeline, where throwing would stop a Player's Turn from ending; and answering 0 would silently give a grown city the territory of a
    /// camp. The caller logs it and takes the single tile.
    /// </remarks>
    /// <returns><see langword="false"/> when no radius is authored for the tier, in which case <paramref name="radius"/> is 0.</returns>
    public static bool TryGetTerritoryRadius(SettlementTier tier, out int radius)
    {
        return _TERRITORY_RADIUS_BY_TIER.TryGetValue(tier, out radius);
    }

    /// <summary>The tiers that can be founded directly, for an error message that lists them.</summary>
    public static IReadOnlyCollection<SettlementTier> FoundableTiers => _DEFINITIONS.Keys;
}

/// <summary>
/// One foundable tier's links out to the action that offers it and the Structure that represents it.
/// </summary>
internal sealed record SettlementTierDefinition
{
    /// <summary>The toolbar action whose strategy owns this tier's rules, and which the settle handler re-runs before founding.</summary>
    public required ForceActionType ActionType { get; init; }

    /// <summary>The <c>structure.Structures</c> code of the Structure that stands on the tile for this tier.</summary>
    public required string StructureCode { get; init; }

    /// <summary>
    /// Whether the player names a Settlement of this tier.
    /// </summary>
    /// <remarks>
    /// A camp is a place the settlers stopped rather than one they founded, so it carries no name and the client never asks for one. The settle handler requires a name
    /// for a named tier and stores none for an unnamed one, so this is the single place the two ends agree on which is which - see <c>Settlement.Name</c>.
    /// </remarks>
    public required bool IsNamed { get; init; }

    /// <summary>
    /// The smallest Force that may found a Settlement of this tier.
    /// </summary>
    /// <remarks>
    /// Read twice, and it has to be the same number both times: the strategy refuses a Force below it, and the settle handler deducts exactly this many troops when the
    /// founders split off rather than being consumed.
    /// </remarks>
    public required int MinimumTroops { get; init; }

    /// <summary>
    /// Whether a large enough Force founds a Settlement of this tier by splitting, rather than by being consumed by it.
    /// </summary>
    /// <remarks>
    /// True for a camp, which is a party that stopped rather than a Force that ended - so a column strong enough to spare them drops off a camp and marches on. False for
    /// a village, which takes everyone who founds it however many they are.
    /// </remarks>
    public required bool SplitsFromFounder { get; init; }

    /// <summary>
    /// Whether a Force of this strength is left standing after founding a Settlement of this tier.
    /// </summary>
    /// <remarks>
    /// <b>Twice the minimum</b>, which is the same as saying the founders only split off when what remains could still found another one. Stating it that way is what
    /// settles the boundary: at exactly twice the minimum the Force survives, because the remainder is still a legal founding party.
    /// </remarks>
    /// <param name="totalStrength">The Force's whole strength, healthy and injured alike.</param>
    public bool WouldSurviveFounding(int totalStrength)
    {
        return SplitsFromFounder && totalStrength >= MinimumTroops * 2;
    }
}
