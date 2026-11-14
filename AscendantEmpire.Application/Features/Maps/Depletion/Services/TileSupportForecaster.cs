using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Forecasts how many Turns a tile can feed a set of Forces standing on it, regrowth included, and the support level that earns.
/// </summary>
/// <remarks>
/// <para>
/// Plays the Turns out against detached copies of the tile's depletion row and its patches, one cycle per Turn, through the same rules the turn pipeline
/// uses:
/// <list type="number">
///   <item><description>The Forces feed from the tile's sources (<see cref="TileFoodSources"/>, split by <see cref="FoodExtractionCalculator"/>), as
///   <c>Gathering</c> feeds them at the end of the Turn. A Turn only counts when the sources cover the whole need.</description></item>
///   <item><description>The tile regrows (<see cref="TileRegenerationCalculator"/>), as <c>RegenerateTileSustenance</c> regrows it at the end of the Round.
///   Always as fed on, since the Forces just fed. A patch does not regrow: MapResource growth is not modelled, so a berry patch eaten is a berry patch
///   gone.</description></item>
/// </list>
/// A Player's feed always comes before the Round's regrowth, whatever the turn order, so feed → regrow → feed holds for every Player.
/// </para>
/// <para>
/// The level, checked in order:
/// <list type="number">
///   <item><description>Nothing available now → <see cref="TileSupportLevel.Exhausted"/>, whatever the need. A stripped tile shows as stripped even with no
///   Forces on it, and even though it will regrow: the Forces on it starve this Turn. A tile whose biome is stripped but which still carries a full patch
///   is not stripped - the Forces on it eat perfectly well.</description></item>
///   <item><description>No need → <see langword="null"/>: there is nobody to feed, so there is nothing to say.</description></item>
///   <item><description>2 Turns or more → <see cref="TileSupportLevel.Plenty"/>; 1 → <see cref="TileSupportLevel.OneTurn"/>; 0 → <see cref="TileSupportLevel.Insufficient"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// The forecast answers "how long can these Forces stay here", and holds everything else fixed: the Forces' size, the Player's modifiers and the biome. It
/// assumes nobody else feeds on the tile, which holds while the Forces stand on it - any other Player stepping on starts a Battle, and Forces in a Battle do
/// not feed. See "Support indicator" in <c>docs/design/campaign-map/support-indicator.md</c>.
/// </para>
/// </remarks>
internal static class TileSupportForecaster
{
    /// <summary>
    /// How many Turns ahead the forecast looks. A tile that still feeds the Forces after this many is reported as lasting beyond the forecast (a null Turns).
    /// </summary>
    public const int MAX_FORECAST_TURNS = 5;

    /// <summary>
    /// How many Turns of need a tile must cover to count as <see cref="TileSupportLevel.Plenty"/>.
    /// </summary>
    private const int _PLENTY_TURNS = 2;

    /// <summary>
    /// Every sustenance resource, in declaration order.
    /// </summary>
    private static readonly SustenanceResource[] _SUSTENANCE_RESOURCES = Enum.GetValues<SustenanceResource>();

    /// <summary>
    /// Forecasts how the tile supports <paramref name="need"/>.
    /// </summary>
    /// <param name="need">The food the Forces need each Turn, in need units. Must not be negative.</param>
    /// <param name="values">The Player's effective sustenance values.</param>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when it has never been fed on. Never changed: the forecast plays out on a copy.</param>
    /// <param name="regeneration">
    /// The biome's regeneration values, or <see langword="null"/> when they could not be found. With no values, or no Regeneration Percent, the tile does not
    /// regrow and the forecast is plain available ÷ need.
    /// </param>
    /// <param name="sustenanceAssetIds">The Asset each biome resource is identified as.</param>
    /// <param name="amountPerUnitByAssetId">What one unit of the need costs in each Asset the Forces' required <c>SupplySet</c> holds.</param>
    /// <param name="mapResourceStocks">The patches standing on the tile. Never changed: the forecast eats copies of them.</param>
    /// <param name="rivers">The tile's four river edges, or <see langword="null"/> when they are not known, which is read as no rivers.</param>
    /// <returns>The level and the Turns, or <see langword="null"/> when there is food but nobody to feed.</returns>
    public static TileSupport? Forecast(
        ScaledAmount need,
        bool isWater,
        PlayerSustenanceValues values,
        BiomeType biome,
        CampaignMapDepletion? depletion,
        BiomeRegenerationValues? regeneration,
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds,
        IReadOnlyDictionary<int, long> amountPerUnitByAssetId,
        IReadOnlyCollection<TileMapResourceStock> mapResourceStocks,
        TileRivers? rivers = null)
    {
        // TODO: The 20% pass-through draw (see Gathering) will let Forces moving through an occupied tile eat from food this forecast treats as the Forces' own. Revisit the
        //       forecast when the draw lands, and when alliances (COMM-4) let another Player's Forces share a tile without a Battle.
        // TODO: A hidden Force feeding here raises the shared Taken, which lowers this forecast (and the toolbar's values) for everyone watching and so signals it is there.
        //       Waits on hiding and detection being modelled - see CM-45.
        // TODO: Once a claimed tile regrows against the claimant's effective Base and Percent (see RegenerateTileSustenance), forecasting on a rival's claimed tile would
        //       reveal how much their tech boosts food. Decide what a non-claimant is shown when Settlements land.
        // TODO: A patch does not grow back here because MapResource growth is not modelled anywhere yet. Count it when it is, or a tile carrying a renewable
        //       patch will read as poorer than it is.

        if (need < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(need), "A food need cannot be negative.");
        }

        // Detached copies, so the forecast can feed and regrow freely without touching the caller's rows (which may be tracked).
        CampaignMapDepletion forecastDepletion = CopyTaken(depletion);
        Dictionary<(int InstanceId, int AssetId), ScaledAmount> patchesLeft = CopyPatches(mapResourceStocks);

        IReadOnlyCollection<FoodSource> sources = GetSources(isWater, values, biome, forecastDepletion, sustenanceAssetIds, amountPerUnitByAssetId, patchesLeft, rivers);
        ScaledAmount totalAvailable = TotalInNeedUnits(sources);

        // First, so a bare tile reads as bare whether or not anyone is standing on it.
        if (totalAvailable == ScaledAmount.Zero)
        {
            return new TileSupport(TileSupportLevel.Exhausted, 0);
        }

        if (need == ScaledAmount.Zero)
        {
            return null;
        }

        int turns = 0;

        // One more Turn than is shown, so a tile that lasts beyond the forecast can be told apart from one that runs out on its last Turn.
        while (turns <= MAX_FORECAST_TURNS && totalAvailable >= need)
        {
            // The end of the Turn: the Forces feed, exactly as Gathering splits the need across the sources.
            FoodExtraction extraction = FoodExtractionCalculator.Extract(need, sources);

            foreach ((FoodSourceKey key, ScaledAmount taken) in extraction.Taken)
            {
                Spend(forecastDepletion, patchesLeft, key, taken);
            }

            turns++;

            // The end of the Round: the biome regrows before the Forces' next feed. The patches do not.
            Regrow(forecastDepletion, regeneration, rivers, isWater);

            sources = GetSources(isWater, values, biome, forecastDepletion, sustenanceAssetIds, amountPerUnitByAssetId, patchesLeft, rivers);
            totalAvailable = TotalInNeedUnits(sources);
        }

        int? forecastTurns = turns > MAX_FORECAST_TURNS ? null : turns;

        return new TileSupport(ClassifyTurns(turns), forecastTurns);
    }

    /// <summary>
    /// Takes one source's share out of the forecast's own copies.
    /// </summary>
    private static void Spend(
        CampaignMapDepletion depletion,
        Dictionary<(int InstanceId, int AssetId), ScaledAmount> patchesLeft,
        FoodSourceKey key,
        ScaledAmount taken)
    {
        if (key.SourceType == FoodSourceType.Tile)
        {
            DepletionTaken.AddTaken(depletion, key.Resource!.Value, taken);

            return;
        }

        (int, int) patchKey = (key.MapResourceInstanceId!.Value, key.AssetId);

        if (patchesLeft.TryGetValue(patchKey, out ScaledAmount left))
        {
            patchesLeft[patchKey] = left > taken ? left - taken : ScaledAmount.Zero;
        }
    }

    /// <summary>
    /// The level earned by feeding the Forces for <paramref name="turns"/> whole Turns, on a tile with some food.
    /// </summary>
    private static TileSupportLevel ClassifyTurns(int turns)
    {
        if (turns >= _PLENTY_TURNS)
        {
            return TileSupportLevel.Plenty;
        }

        if (turns == 1)
        {
            return TileSupportLevel.OneTurn;
        }

        return TileSupportLevel.Insufficient;
    }

    /// <summary>
    /// The tile's sources as the forecast's copies currently stand.
    /// </summary>
    private static IReadOnlyCollection<FoodSource> GetSources(
        bool isWater,
        PlayerSustenanceValues values,
        BiomeType biome,
        CampaignMapDepletion depletion,
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds,
        IReadOnlyDictionary<int, long> amountPerUnitByAssetId,
        Dictionary<(int InstanceId, int AssetId), ScaledAmount> patchesLeft,
        TileRivers? rivers)
    {
        TileMapResourceStock[] stocks =
        [
            .. patchesLeft.Select(entry => new TileMapResourceStock(entry.Key.InstanceId, entry.Key.AssetId, entry.Value, IsYieldEnabled: true))
        ];

        return TileFoodSources.GetAvailable(isWater, values, biome, depletion, sustenanceAssetIds, amountPerUnitByAssetId, stocks, rivers);
    }

    /// <summary>
    /// How many individuals everything on the tile could feed for one Turn.
    /// </summary>
    private static ScaledAmount TotalInNeedUnits(IReadOnlyCollection<FoodSource> sources)
    {
        return sources.Aggregate(ScaledAmount.Zero, (sum, source) => sum + source.ContributionInNeedUnits);
    }

    /// <summary>
    /// One end-of-Round regrowth of every resource the biome has, as fed on.
    /// </summary>
    /// <remarks>
    /// Every resource with a base, not only the ones the tile offers, so the copy regrows exactly as <c>RegenerateTileSustenance</c> would regrow the tile - the tile's
    /// rivers included, or a riverbank would be forecast to give up Fish it never got back.
    /// </remarks>
    private static void Regrow(CampaignMapDepletion depletion, BiomeRegenerationValues? regeneration, TileRivers? rivers, bool isWater)
    {
        // Legacy data with no Percent is skipped at the end of the Round too, so the forecast does not regrow it either.
        if (regeneration?.RegenerationPercent is null)
        {
            return;
        }

        foreach (SustenanceResource resource in _SUSTENANCE_RESOURCES)
        {
            // The base a riverbank regrows against is the biome's plus whatever its rivers add, so Fish a river put on dry land comes back like anything else.
            ScaledAmount? baseValue = RiverSustenanceBonus.Apply(regeneration.GetBaseValue(resource), resource, rivers, isWater);

            if (baseValue is null)
            {
                continue;
            }

            ScaledAmount newTaken = TileRegenerationCalculator.Regenerate(
                DepletionTaken.GetTaken(depletion, resource),
                baseValue.Value,
                regeneration.RegenerationPercent.Value,
                fedOn: true);

            DepletionTaken.SetTaken(depletion, resource, newTaken);
        }
    }

    /// <summary>
    /// A detached depletion row holding the same amounts taken as <paramref name="depletion"/>, or nothing taken when the tile has no row.
    /// </summary>
    private static CampaignMapDepletion CopyTaken(CampaignMapDepletion? depletion)
    {
        if (depletion is null)
        {
            return new CampaignMapDepletion();
        }

        return new CampaignMapDepletion
        {
            CampaignMapId = depletion.CampaignMapId,
            GameId = depletion.GameId,
            ForageTaken = depletion.ForageTaken,
            HuntTaken = depletion.HuntTaken,
            FishTaken = depletion.FishTaken,
            FodderTaken = depletion.FodderTaken,
            FedOnRound = depletion.FedOnRound
        };
    }

    /// <summary>
    /// What each patch on the tile holds, as the forecast's own copy to eat down.
    /// </summary>
    /// <remarks>
    /// A patch the Player cannot take the yield of, or one holding its amount in child rows, is left out here rather than carried and skipped every cycle.
    /// </remarks>
    private static Dictionary<(int InstanceId, int AssetId), ScaledAmount> CopyPatches(IReadOnlyCollection<TileMapResourceStock> stocks)
    {
        return stocks
            .Where(stock => stock.IsYieldEnabled && stock.Available is not null)
            .ToDictionary(stock => (stock.MapResourceInstanceId, stock.AssetId), stock => stock.Available!.Value);
    }
}
