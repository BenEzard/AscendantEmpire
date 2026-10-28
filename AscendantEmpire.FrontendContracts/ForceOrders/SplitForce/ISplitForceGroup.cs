namespace AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

/// <summary>
/// One of the Forces a completed split produced.
/// </summary>
public interface ISplitForceGroup
{
    /// <summary>The group's Force id — the original Force's own id for the group it kept, and a newly created one for each of the others.</summary>
    int UnitInstanceId { get; }

    /// <summary>The group's strength, healthy and injured together.</summary>
    int Size { get; }

    /// <summary>How many of the group's soldiers are unwounded.</summary>
    int HealthyCount { get; }

    /// <summary>How many of the group's soldiers are wounded.</summary>
    int InjuredCount { get; }

    /// <summary>
    /// Whether this group is the Force that was split, rather than one created by the split.
    /// </summary>
    /// <remarks>
    /// Exactly one group has this. Splitting does not destroy and recreate the Force — the original survives holding the first group — so this is what lets
    /// the client keep the player's selection on the Force they were already holding instead of guessing.
    /// </remarks>
    bool IsOriginalForce { get; }
}
