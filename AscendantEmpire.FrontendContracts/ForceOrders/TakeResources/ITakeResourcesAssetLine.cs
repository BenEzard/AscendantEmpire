namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// One Asset a take would put into a Force's cargo, or did.
/// </summary>
/// <remarks>
/// A MapResourceType can give several Assets at once - a Deer gives game and a pelt - so one take produces a line per Asset the Player's switches leave on.
/// A yield they have switched off is absent rather than listed at zero: they do not receive it, and the tile is charged for it all the same.
/// </remarks>
public interface ITakeResourcesAssetLine
{
    int AssetId { get; }

    string AssetDisplayName { get; }

    /// <summary>
    /// How much of the Asset, in whole units, rounded down.
    /// </summary>
    /// <remarks>
    /// Whole rather than fractional, matching the asset ticker: the balance keeps thousandths so a part unit accumulates, but what the player is told is
    /// what they could spend.
    /// </remarks>
    int Amount { get; }
}
