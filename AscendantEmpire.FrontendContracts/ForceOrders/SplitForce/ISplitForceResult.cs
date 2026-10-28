namespace AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

/// <summary>
/// What a split order produced: every Force now standing where the one that split was.
/// </summary>
/// <remarks>
/// <para>
/// No map payload, matching <c>ISettleForceResult</c>. A split changes the tile by adding Forces to it and reducing the one already there, and the client
/// refetches its view rather than being handed a patch to merge.
/// </para>
/// <para>
/// Splitting costs the whole turn, so every group comes back with no movement remaining.
/// </para>
/// </remarks>
public interface ISplitForceResult
{
    /// <summary>The tile every group is standing on — the one the Force split on.</summary>
    int XCoord { get; }

    /// <summary>The tile every group is standing on — the one the Force split on.</summary>
    int YCoord { get; }

    /// <summary>
    /// The Forces the split produced, in the order the requested sizes were given.
    /// </summary>
    /// <remarks>The first is the original Force, now holding only its own group; see <see cref="ISplitForceGroup.IsOriginalForce"/>.</remarks>
    IReadOnlyCollection<ISplitForceGroup> Groups { get; }
}
