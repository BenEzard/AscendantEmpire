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
    public required long MovementPointsRemaining { get; init; }

    /// <summary>
    /// The Force's full per-turn movement allowance, copied from its compiled template. Raw fixed-point on the same Scale 1,000 convention as
    /// <see cref="MovementPointsRemaining"/>, so the two compare directly.
    /// </summary>
    public required long CampaignMovementPoints { get; init; }

    /// <summary>
    /// The military organisation tag resolved onto the Force's compiled template, or null when its template carries none.
    /// </summary>
    public required ForceActionOrganisation? Organisation { get; init; }

    /// <summary>The Force's current size — every soldier still standing, hurt or not.</summary>
    public int TotalStrength => HealthyCount + InjuredCount;

    /// <summary>Whether the Force still holds every movement point it started the turn with.</summary>
    /// <remarks>
    /// Actions which cost a whole turn require this rather than merely "some movement left". Note that nothing currently initialises or
    /// replenishes <c>MovementPointsRemaining</c>, so in practice this reads false until that is implemented — see the design doc.
    /// </remarks>
    public bool HasFullMovement => MovementPointsRemaining >= CampaignMovementPoints;
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
