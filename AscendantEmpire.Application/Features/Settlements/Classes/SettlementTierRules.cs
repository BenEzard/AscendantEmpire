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
/// The capability tag and the troop minimum are deliberately <b>not</b> here. Those belong to the strategies, which already own them and are already re-run at execution
/// time through <c>IForceContextActionService.EvaluateActionAsync</c>; restating them would be a second copy of a rule that can then drift.
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
            IsNamed = false
        },
        [SettlementTier.Village] = new SettlementTierDefinition
        {
            ActionType = ForceActionType.CreateVillage,
            StructureCode = StructureSeedData.VILLAGE_CODE,
            IsNamed = true
        },
    };

    /// <summary>
    /// The definition for a tier, or <see langword="null"/> when the tier cannot be founded directly.
    /// </summary>
    public static SettlementTierDefinition? For(SettlementTier tier)
    {
        return _DEFINITIONS.GetValueOrDefault(tier);
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
}
