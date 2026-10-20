using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a Force can found a village on the tile it stands on.
/// </summary>
/// <remarks>
/// <para>
/// Unlocked by the <c>create-settlement</c> capability, which the settler's <c>camping-materials</c> equipment component carries. A Force whose
/// template grants no such capability hides the action rather than offering a disabled button, because carrying camping gear is not something the
/// player can change about a Force already in the field.
/// </para>
/// <para>
/// Everything else — the terrain, the movement and the size of the Force — is mutable state the player can act on, so it disables the action and
/// says why. Those rules live in <see cref="SettlementFoundingRules"/>, shared with <see cref="EstablishCampStrategy"/>.
/// </para>
/// <para>
/// The capability tags are <b>catalogue data</b>: seeded from <c>TagSeedData</c>, and editable through the Entity Setup Tool afterwards. On a
/// database where the Tags seed has not run and nobody has authored them, no template compiles with them and this action simply never appears —
/// which is correct, not a fault.
/// </para>
/// </remarks>
internal sealed class CreateVillageStrategy : IForceActionStrategy
{
    /// <summary>What is being founded, for the message text.</summary>
    private const string _SETTLEMENT_NOUN = "village";

    /// <summary>
    /// The smallest Force that can found a village — four of the settler organisation's 25-strong base groups.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="SettlementTierRules"/>, where every tier's minimum now lives so that the strategy and the settle handler cannot disagree about it. A village
    /// consumes whoever founds it however large they are, so unlike a camp nothing here is ever deducted.
    /// </remarks>
    private static int MinimumTroops => SettlementTierRules.For(SettlementTier.Village)!.MinimumTroops;

    public ForceActionType ActionType => ForceActionType.CreateVillage;

    public string Label => "Create Village";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ForceActionForce force = context.SelectedForce;

        // Structural: without the capability the Force could never found a village, wherever it moves and however many troops it gathers.
        // Hidden rather than disabled, exactly as SplitForceStrategy hides itself for a commissioned organisation.
        if (!force.HasCapability(CapabilityTagCodes.CREATE_SETTLEMENT))
        {
            return null;
        }

        // Blocked with no reasons falls back to Available, so a clean pass needs no separate branch.
        return ForceActionAvailability.Blocked(
            SettlementFoundingRules.CollectBlockingReasons(force, _SETTLEMENT_NOUN, MinimumTroops));
    }
}
