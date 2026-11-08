namespace AscendantEmpire.BackendContracts.MapResources.GetPlayerMapResources;

/// <summary>
/// One MapResource instance on one tile, as the Player who can see it is told about it.
/// </summary>
/// <remarks>
/// Carries the type's own display values rather than only its id, so a tile can be drawn without the client first fetching the catalogue.
/// </remarks>
public record PlayerMapResourceEntry
{
    public required int X { get; init; }

    public required int Y { get; init; }

    /// <summary>
    /// Which of the tile's two places the instance takes, so two resources on one tile keep a stable order.
    /// </summary>
    public required byte Slot { get; init; }

    public required int MapResourceTypeId { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// The image to draw on the tile, or <see langword="null"/> for a type that is already said by its Biome.
    /// </summary>
    public required string? IconPath { get; init; }

    /// <summary>
    /// Whether one of the Player's Observers can see the tile at this moment.
    /// </summary>
    /// <remarks>
    /// The MapResource is reported either way, because the Player has discovered it. This says only whether <see cref="EstimatedQuantity"/> could come with
    /// it, which is what lets a client draw "seen, but no number given" differently from "watched, and this is how much".
    /// </remarks>
    public required bool IsObserved { get; init; }

    /// <summary>
    /// What this Player is shown in place of the true quantity, scaled by <c>ScaledAmount.Scale</c>.
    /// </summary>
    /// <remarks>
    /// Null where there is no number to give: the tile is not observed, a stratified instance holds its quantity in layers the Player must survey, a
    /// Distribution holds its own in per-Asset amounts, or the type was authored with no fuzzy range. The true quantity is never sent.
    /// </remarks>
    public required long? EstimatedQuantity { get; init; }
}
