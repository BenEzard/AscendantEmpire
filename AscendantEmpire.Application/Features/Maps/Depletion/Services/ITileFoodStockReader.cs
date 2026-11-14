using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Reads the MapResource stock standing on campaign map tiles: which Assets each patch holds for a Player, and how much.
/// </summary>
/// <remarks>
/// <para>
/// The one read of that stock, shared by the end-of-Turn feed (<c>Gathering</c>) and the support indicator
/// (<see cref="ITileSupportEvaluator"/>), so a tile can never be forecast to feed more than it really will.
/// </para>
/// <para>
/// Discovery is deliberately not a condition. A patch is physical food, and a Force camped on it eats whether or not anyone has put it on the Player's map;
/// Forces starving beside berries nobody surveyed would be the stranger rule.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface ITileFoodStockReader
{
    /// <summary>
    /// The stock on each of <paramref name="tileIds"/>, keyed by tile. A tile with nothing on it is absent.
    /// </summary>
    /// <param name="playerId">The Player whose yield switches decide which Assets a patch gives.</param>
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<TileMapResourceStock>>> GetStocksAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<int> tileIds,
        CancellationToken cancelToken = default);
}
