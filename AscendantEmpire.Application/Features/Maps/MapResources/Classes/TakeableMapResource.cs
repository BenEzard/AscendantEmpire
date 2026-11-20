namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <summary>
/// One MapResource standing on a tile that a Force could take from, and what taking a unit of it gives.
/// </summary>
/// <remarks>
/// <para>
/// Flat, with no EF entity on it, because the context-action strategies that read it are pure synchronous functions - see
/// <c>IForceActionStrategy</c>. The handler that writes loads the instance itself.
/// </para>
/// <para>
/// Only an instance holding a quantity of its own reaches here. A Distribution or a stratified instance keeps its amount in child rows, so it has no
/// <c>Quantity</c> to take against, and neither kind is takeable yet - the same two <c>TileFoodStockReader</c> skips when it prices a tile's food.
/// </para>
/// </remarks>
internal sealed record TakeableMapResource(
    int MapResourceInstanceId,
    int MapResourceTypeId,
    byte Slot,
    string DisplayName,
    string? IconPath,
    long QuantityRaw,
    IReadOnlyCollection<TakeableMapResourceYield> Yields);

/// <summary>
/// One Asset a takeable MapResource gives, at the rate its type declares.
/// </summary>
/// <remarks>
/// Only the Assets this Player actually receives: a yield their switches leave off never reaches here, and an instance left with no yield at all is not
/// takeable, since taking it would empty the tile for nothing.
/// </remarks>
internal sealed record TakeableMapResourceYield(int AssetId, string AssetDisplayName, long AmountPerUnit);
