using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Feeds Forces out of the food they are carrying.
/// </summary>
/// <remarks>
/// <para>
/// A Force eats everything its tile offers - base values and MapResources <b>first</b>, - and only the remaining need is taken from what they're carrying.
/// </para>
/// <para>
/// <b>Why not <see cref="IAssetBankDrawdownService"/>.</b> That service is the single spender, and it filters cargo out of both its arms on purpose: a load is not stock the 
/// Empire can spend.
/// </para>
/// <para>
/// <b>Local-first, then the network</b>, the same ordering a Settlement's expense uses: a Force eats its own load before a Unit sharing its
/// <c>NetworkKey</c> is asked for anything. A key of <see cref="Guid.Empty"/> is the absence of a network rather than one two banks share, so an unkeyed bank
/// reaches nothing but itself.
/// </para>
/// <para>See "Force cargo" in <c>docs/design/asset-banks.md</c>.</para>
/// </remarks>
internal interface ICarriedFoodConsumptionService
{
    /// <summary>
    /// Feeds each draw's Force as far as what it is carrying allows, and answers the rations each one got.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> <c>Gathering</c> calls this before its own save, so the food leaving a load, the food leaving the tile and the casualties of
    /// going short all commit together - a Turn can never take the load without the feeding, or the feeding without the load.
    /// <para>
    /// Draws are served in Force id order, so where two Forces share a network the earlier one eats first and the later one sees what is left.
    /// </para>
    /// </remarks>
    /// <param name="playerId">The Player whose Forces these are, so a load of another Player's can never be eaten.</param>
    /// <param name="amountsPerUnitBySetId">
    /// What one ration costs in each Asset, by food set id and then by Asset id, as <see cref="Maps.Depletion.Services.ISupplySetMembershipReader"/> gives it. 
    /// Passed in because the caller has already read it for the tile feed.
    /// </param>
    /// <returns>The rations fed, by Force id. A Force that ate nothing is left out rather than carried as a zero.</returns>
    Task<IReadOnlyDictionary<int, ScaledAmount>> ConsumeAsync(
        AscendantEmpireContext context,
        int playerId,
        IReadOnlyCollection<CarriedFoodDraw> draws,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> amountsPerUnitBySetId,
        CancellationToken cancelToken = default);
}
