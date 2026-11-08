namespace AscendantEmpire.FrontendContracts.MapResources.GetPlayerMapResources;

/// <summary>
/// One MapResource on one tile, as the Player who can see it is told about it.
/// </summary>
public interface IPlayerMapResourceEntry
{
    int X { get; }

    int Y { get; }

    /// <summary>
    /// Which of the tile's two places the instance takes, so two resources on one tile keep a stable order.
    /// </summary>
    byte Slot { get; }

    int MapResourceTypeId { get; }

    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// The image to draw on the tile, or <see langword="null"/> for a type that is already said by its Biome.
    /// </summary>
    string? IconPath { get; }

    /// <summary>
    /// Whether one of the Player's Observers can see the tile right now.
    /// </summary>
    /// <remarks>
    /// The MapResource is sent either way, because the Player has discovered it and it stays on their map. This says only whether a quantity could come with
    /// it, so a tile can draw the icon alone without implying the patch is empty.
    /// </remarks>
    bool IsObserved { get; }

    /// <summary>
    /// What this Player is shown in place of the true quantity, in whole units.
    /// </summary>
    /// <remarks>
    /// An estimate, not the truth: it is the quantity moved by this Player's own fixed error, within the instance's fuzzy factor. Null where there is no
    /// number to give — the tile is not observed, a stratified instance's quantity is in layers that must be surveyed, or a Distribution's is in per-Asset
    /// amounts.
    /// </remarks>
    decimal? EstimatedQuantity { get; }
}
