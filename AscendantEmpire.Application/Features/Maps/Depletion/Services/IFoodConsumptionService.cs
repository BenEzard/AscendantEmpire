using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Spends what Forces ate: raises a tile's amounts taken, and lowers the quantity of every MapResource patch they stripped.
/// </summary>
/// <remarks>
/// <para>
/// The one place food leaves the map. Split from the feed itself because the two sources are stored quite differently - a biome holds a rate that regrows
/// and a patch holds a stock that does not - and because an extraction mine or a gathering order will spend a patch the same way without feeding anybody.
/// </para>
/// <para>
/// Backend-only: there is no endpoint, and nothing a Player sends decides what is spent.
/// </para>
/// </remarks>
internal interface IFoodConsumptionService
{
    /// <summary>
    /// Spends everything in <paramref name="consumptions"/> against the caller's context.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> The turn pipeline is not transactional, so a save of its own would let a tile be charged for a feed whose casualties then
    /// failed to write. The caller saves once, with everything the feed did in it.
    /// </remarks>
    /// <param name="gameId">The Game the tiles are in, so a patch in another Game can never be charged.</param>
    Task ConsumeAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<TileFoodConsumption> consumptions,
        CancellationToken cancelToken = default);
}
