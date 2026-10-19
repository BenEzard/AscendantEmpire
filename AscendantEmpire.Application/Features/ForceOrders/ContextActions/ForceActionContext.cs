using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// An immutable snapshot of everything the context-action strategies need to judge a selected Force.
/// </summary>
/// <remarks>
/// <para>
/// Built <b>once</b> per request and handed to every strategy, so the strategies themselves are pure synchronous functions and the toolbar costs
/// a fixed number of queries no matter how many action types exist. This differs deliberately from the end-turn
/// <c>IPossibleActionCheck</c> pipeline, where each check opens its own context — acceptable once per turn, but not on every Force click.
/// </para>
/// </remarks>
internal sealed record ForceActionContext
{
    /// <summary>The Force the player has selected, and whose toolbar is being built.</summary>
    public required ForceActionForce SelectedForce { get; init; }

    /// <summary>
    /// The other Forces sharing the selected Force's campaign map tile, excluding the selection itself.
    /// </summary>
    /// <remarks>Empty when the Force stands alone. Carried for the actions which combine or transfer between Forces.</remarks>
    public required IReadOnlyCollection<ForceActionForce> OtherForcesOnTile { get; init; }
}

/// <summary>
/// One Force as the context-action strategies see it: its current strength and movement, plus the parts of its compiled template that bear on
/// what it may do.
/// </summary>
internal sealed record ForceActionForce
{
    public required int UnitInstanceId { get; init; }

    public required int PlayerId { get; init; }

    public required int CampaignMapTileId { get; init; }

    /// <summary>The Force's own name, or the compiled template's display name when it has not been named.</summary>
    public required string DisplayName { get; init; }

    public required int HealthyCount { get; init; }

    public required int InjuredCount { get; init; }

    /// <summary>Movement points left this turn, raw fixed-point on the Scale 1,000 convention.</summary>
    public required long CampaignMovementRemaining { get; init; }

    /// <summary>
    /// The Force's full per-turn movement allowance, copied from its compiled template. Raw fixed-point on the same Scale 1,000 convention as
    /// <see cref="CampaignMovementRemaining"/>, so the two compare directly.
    /// </summary>
    public required long CampaignMovementPoints { get; init; }

    /// <summary>
    /// The military organisation tag resolved onto the Force's compiled template, or null when its template carries none.
    /// </summary>
    public required ForceActionOrganisation? Organisation { get; init; }

    /// <summary>
    /// The capability tag codes the Force's compiled template grants, unioned across its components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is how an action tied to a piece of equipment finds out whether the Force is carrying it — <c>create-early-camp</c> from the settler's camping
    /// materials, for example. Matching a capability rather than a component code or a template id means a second component granting the same
    /// ability, or a rename of the one that does today, costs no code change.
    /// </para>
    /// <para>
    /// Empty rather than null when the template grants nothing, so a strategy never has to null-check before asking.
    /// </para>
    /// </remarks>
    public required IReadOnlyCollection<string> CapabilityTagCodes { get; init; }

    /// <summary>Whether the biome of the tile the Force stands on is water.</summary>
    /// <remarks>
    /// Only the biome's own flag, deliberately not the broader "is this tile water" used by movement, which also counts the DeepWater and
    /// ShallowWater geographies. The two questions are different — whether a Force may <i>enter</i> a tile, versus whether it may <i>settle</i> it —
    /// so they are kept apart rather than sharing one definition that a later movement change could silently widen.
    /// </remarks>
    public required bool TileIsWater { get; init; }

    /// <summary>The geography of the tile the Force stands on.</summary>
    public required CampaignMapGeographyType TileGeographyType { get; init; }

    /// <summary>Whether the Force is fighting in a Battle, which is to say standing on a battle map tile.</summary>
    /// <remarks>
    /// Read off the Force's battle map tile rather than by looking for an open Battle on its campaign tile, because the tile is what every other part of the game treats as "in battle" - it is
    /// what refuses the Force a campaign step - and the two must never disagree about the same Force.
    /// </remarks>
    public required bool IsInBattle { get; init; }

    /// <summary>The Force's current size — every soldier still standing, hurt or not.</summary>
    public int TotalStrength => HealthyCount + InjuredCount;

    /// <summary>Whether the Force's compiled template grants the named capability.</summary>
    /// <remarks>Ordinal comparison: these are stable authored codes, not display text, so culture must play no part in matching them.</remarks>
    public bool HasCapability(string tagCode)
    {
        return CapabilityTagCodes.Contains(tagCode, StringComparer.Ordinal);
    }

    /// <summary>Whether the Force is holding at least a full Turn's movement allowance.</summary>
    /// <remarks>
    /// <para>
    /// Actions which cost a whole Turn require this rather than merely "some movement left".
    /// </para>
    /// <para>
    /// <b>This is no longer proof that the Force has not moved this Turn.</b> It used to be: before F-14 the remaining value could never exceed the
    /// allowance, so the comparison was an equality in all but name. Now that unspent movement is carried into the next Turn and added on top of the
    /// allowance, a Force can open its Turn above it — and having moved, still sit at or above it. Tightening this needs the total the Force was
    /// actually granted this Turn, which is not stored, so it is knowingly left loose. See the F-14 follow-up.
    /// </para>
    /// </remarks>
    public bool HasFullMovement => CampaignMovementRemaining >= CampaignMovementPoints;
}

/// <summary>
/// The authored sizing and leadership rules of a Force's military organisation tag.
/// </summary>
/// <remarks>Flattened off the tag so the strategies never touch an EF entity, keeping them pure and trivially testable.</remarks>
internal sealed record ForceActionOrganisation
{
    public required int TagId { get; init; }

    /// <summary>The tag's stable code, e.g. <c>settler</c>.</summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>How formations under this tag are led. Null is treated as commissioned.</summary>
    public required LeadershipModel? LeadershipModel { get; init; }

    /// <summary>The indivisible group size for an unled tag; null otherwise.</summary>
    public required int? UnledBaseSize { get; init; }

    /// <summary>The largest a single unled Force may be; null otherwise.</summary>
    public required int? UnledMaxSize { get; init; }

    /// <summary>Whether this tag's formations are unled, and so sized in whole multiples of <see cref="UnledBaseSize"/>.</summary>
    public bool IsUnled => LeadershipModel == Domain.Enums.UnitDesigns.LeadershipModel.Unled;
}
