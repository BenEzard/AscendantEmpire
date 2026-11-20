namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// The body of a take-resources order: which Forces are loading up, and which MapResource on their tile they take from.
/// </summary>
/// <remarks>
/// <para>
/// The game and player are deliberately absent - they come from the caller's game token, so an order cannot be aimed at another player's Forces. The tile
/// is absent for the same reason a split carries none: the resource is taken where the Forces already stand, and naming a tile would let a caller take from
/// one their Forces are nowhere near.
/// </para>
/// <para>
/// Unlike a split or a founding, this covers the <b>whole selection</b>. Nothing about the order is per Force - there is one resource and one rate - so the
/// toolbar's usual reading holds: an action offered over a selection is one order covering all of them.
/// </para>
/// </remarks>
public interface ITakeResourcesDto
{
    /// <summary>
    /// The Forces taking. Read these as Force ids: they are <c>UnitInstance</c> ids while Units are the only Force ever fielded, and will be Command ids too
    /// once Commands are - see <c>docs/Terminology.md</c>.
    /// </summary>
    /// <remarks>
    /// Every one must exist, still be standing, belong to the caller and be on the tile the named resource is on. The Forces take in ascending id against
    /// the resource's dwindling quantity, so a later Force may find nothing left.
    /// </remarks>
    IReadOnlyCollection<int> ForceIds { get; }

    /// <summary>
    /// Which MapResource on the tile to take from, by instance id.
    /// </summary>
    /// <remarks>
    /// A tile holds up to two, so the choice is the player's rather than the server's. It must be one the options endpoint offered for this selection:
    /// anything else is refused rather than silently swapped for whatever else is there.
    /// </remarks>
    int MapResourceInstanceId { get; }
}
