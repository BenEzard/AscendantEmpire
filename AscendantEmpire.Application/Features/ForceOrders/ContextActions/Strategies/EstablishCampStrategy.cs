using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;

/// <summary>
/// Decides whether a Force can establish a camp on the tile it stands on.
/// </summary>
/// <remarks>
/// <para>
/// The smaller sibling of <see cref="CreateVillageStrategy"/>: same terrain and movement rules, a far lower troop requirement, and its own
/// capability — <c>found-camp</c>, carried by the settler's <c>camping-materials</c> equipment component alongside <c>found-settlement</c>.
/// </para>
/// <para>
/// Two capabilities rather than one is what lets a future component grant camping without granting settlement, which is the whole point of gating
/// on capabilities instead of on the component that happens to carry them today.
/// </para>
/// </remarks>
internal sealed class EstablishCampStrategy : IForceActionStrategy
{
    /// <summary>The capability that unlocks the action. Stable authored code; a rename in the catalogue is a rename here.</summary>
    private const string _CAPABILITY_TAG_CODE = "found-camp";

    /// <summary>What is being founded, for the message text.</summary>
    private const string _SETTLEMENT_NOUN = "camp";

    /// <summary>The smallest Force that can establish a camp — one of the settler organisation's 25-strong base groups.</summary>
    private const int _MINIMUM_TROOPS = 25;

    public ForceActionType ActionType => ForceActionType.EstablishCamp;

    public string Label => "Establish Camp";

    public ForceActionAvailability? Evaluate(ForceActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ForceActionForce force = context.SelectedForce;

        // Structural: without the capability the Force could never establish a camp, so the button is hidden rather than greyed out.
        if (!force.HasCapability(_CAPABILITY_TAG_CODE))
        {
            return null;
        }

        // Blocked with no reasons falls back to Available, so a clean pass needs no separate branch.
        return ForceActionAvailability.Blocked(
            SettlementFoundingRules.CollectBlockingReasons(force, _SETTLEMENT_NOUN, _MINIMUM_TROOPS));
    }
}
