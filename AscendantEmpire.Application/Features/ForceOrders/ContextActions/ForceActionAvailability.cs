namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// A strategy's verdict on one action: whether the player may take it right now, and if not, every reason why.
/// </summary>
/// <remarks>
/// <para>
/// A strategy returning <see langword="null"/> instead of one of these means something different again — that the action is structurally
/// irrelevant to the Force and should not appear in the toolbar at all. See <see cref="IForceActionStrategy.Evaluate"/>.
/// </para>
/// </remarks>
internal sealed record ForceActionAvailability
{
    /// <summary>Whether the action can be taken right now.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Every reason the action is currently unavailable, in a fixed order. Empty when <see cref="IsEnabled"/> is true.
    /// </summary>
    /// <remarks>
    /// All failing rules are reported together rather than only the first, so a player who is both too small to split and out of movement is
    /// told both at once instead of fixing one to discover the other.
    /// </remarks>
    public required IReadOnlyCollection<string> DisabledReasons { get; init; }

    /// <summary>The action is available; no reasons to give.</summary>
    public static ForceActionAvailability Available()
    {
        return new ForceActionAvailability { IsEnabled = true, DisabledReasons = [] };
    }

    /// <summary>
    /// The action applies to this Force but is blocked, for the reasons given.
    /// </summary>
    /// <remarks>Falls back to <see cref="Available"/> when handed no reasons, so a rule set that finds no fault cannot produce a button that is
    /// disabled with no explanation.</remarks>
    public static ForceActionAvailability Blocked(IReadOnlyCollection<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        if (reasons.Count == 0)
        {
            return Available();
        }

        return new ForceActionAvailability { IsEnabled = false, DisabledReasons = reasons };
    }
}
