namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// What a take-resources order produced: what came off the tile, and what each Force is now carrying extra.
/// </summary>
/// <remarks>
/// <para>
/// No map payload, matching <c>ISettleForceResult</c> and <c>ISplitForceResult</c>. A take lowers a MapResource's quantity and spends the Forces' movement,
/// and the client refetches its view rather than being handed a patch to merge - which is also how the tile's estimate and its food figure come back right.
/// </para>
/// <para>
/// Taking costs the whole turn, so every Force that took anything comes back with no movement remaining.
/// </para>
/// </remarks>
public interface ITakeResourcesResult
{
    /// <summary>The MapResourceType's name, for what the player is told.</summary>
    string DisplayName { get; }

    /// <summary>The tile it was taken on.</summary>
    int XCoord { get; }

    /// <summary>The tile it was taken on.</summary>
    int YCoord { get; }

    /// <summary>
    /// How many whole units came off the MapResource, across every Force in the order.
    /// </summary>
    int UnitsTaken { get; }

    /// <summary>
    /// Whether the MapResource is now empty.
    /// </summary>
    /// <remarks>
    /// A renewable one is not gone: it goes dormant and may come back at the end of a round. Reported because the player has just emptied it themselves, so
    /// it discloses nothing they do not already know.
    /// </remarks>
    bool IsExhausted { get; }

    /// <summary>
    /// What went into cargo, a line per Asset, summed across the whole selection.
    /// </summary>
    IReadOnlyCollection<ITakeResourcesAssetLine> Gained { get; }

    /// <summary>
    /// What each Force in the order came away with, in the order they took - ascending Force id.
    /// </summary>
    /// <remarks>
    /// Carried so the player can be told who got nothing. The Forces draw on one dwindling quantity, so a large selection on a small patch leaves the later
    /// ones empty-handed, and a Force that took nothing is also one that spent no movement.
    /// </remarks>
    IReadOnlyCollection<ITakeResourcesForceOutcome> Forces { get; }
}
