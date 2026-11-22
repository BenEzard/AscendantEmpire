using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;

/// <summary>
/// Draws every Settlement's food and materials off the tiles of its own territory at the start of the Round, and banks what it draws.
/// </summary>
/// <remarks>
/// <para>
/// The territory is the tiles carrying the Settlement in <see cref="CampaignMap.OwnedBySettlementId"/>, so a camp draws from the single tile it stands on and
/// a Settlement whose construction has not finished draws from nothing - <c>BuildingStructure</c> does not write the territory until it completes. See
/// "Harvest" in <c>docs/design/settlements.md</c>.
/// </para>
/// <para>
/// <b>Sustenance is capped at what the tile regrows</b>, through the same <see cref="TileRegenerationCalculator.CalculateRegrowth"/> the end-of-Round
/// replenishment gives back, and what is taken is added to the tile's shared <c>Taken</c>. The two cancel, so a Settlement's territory ends the Round as full
/// as it started and a Force still finds the standing stock. A Settlement allowed to take everything available would strip its territory in one Round and
/// then receive exactly this much anyway, so the cap costs it nothing and leaves the land alive.
/// </para>
/// <para>
/// <b>Materials are taken at the biome's base value, every Round.</b> Nothing is recorded against the tile and nothing regrows, because a material value is
/// static - see <see cref="MaterialResource"/>.
/// </para>
/// <para>
/// <b>The tile is only charged if the Settlement could bank the harvest.</b> Each Settlement's credit goes over on its own, so one Settlement with no bank
/// does not stop the rest of the Game harvesting, and a Settlement whose credit failed has taken nothing off its tiles either.
/// </para>
/// <para>
/// <b>A harvest does not mark the tile as fed on.</b> <see cref="CampaignMapDepletion.FedOnRound"/> records that a Force was present, which is what the 10%
/// floor of the replenishment rules turns on.
/// </para>
/// <para>
/// Game-scoped: the start of a Round is not inside anybody's Turn, so this runs once for every Settlement in the Game rather than for one Player's.
/// </para>
/// </remarks>
internal class HarvestSettlementTerritory(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IPlayerSustenanceValueReader sustenanceValueReader,
    IBiomeRegenerationValueReader biomeRegenerationValueReader,
    ISustenanceAssetReader sustenanceAssetReader,
    IMaterialAssetReader materialAssetReader,
    ISightDataReader sightDataReader,
    ICampaignRiverReader campaignRiverReader,
    IAssetBankCreditService creditService,
    ILogger<HarvestSettlementTerritory> logger) : IStartCampaignRoundProcess
{
    /// <summary>
    /// Every sustenance resource, in declaration order.
    /// </summary>
    private static readonly SustenanceResource[] _SUSTENANCE_RESOURCES = Enum.GetValues<SustenanceResource>();

    /// <summary>
    /// Every material resource, in declaration order.
    /// </summary>
    private static readonly MaterialResource[] _MATERIAL_RESOURCES = Enum.GetValues<MaterialResource>();

    public string ItemName => "Harvest Settlement Territory";

    public int ExecutionOrder => (int)StartCampaignRoundProcessOrder.HarvestSettlementTerritory;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // TODO: The MapResources standing on a territory tile are not harvested. A patch of berries or timber on owned ground is taken by a Force through
        //       TakeResources and by nothing else - see "Not part of this model" in docs/design/settlements.md.
        // TODO: A hostile Force on a territory tile should deny the Settlement the food it was drawing from it, which is what makes a ring of occupied tiles a
        //       siege. Here the Settlement always gets first claim, because it harvests before anybody moves. Waits on occupation being read here.
        // TODO: Nothing is reported to the Player. What a Settlement gathered, and what was wasted for want of storage, exist only in this log. A generic
        //       reporting mechanism is wanted rather than a harvest-shaped one.
        // TODO: Nothing consumes the harvest. A population is meant to eat out of the bank at the end of the Round, and the surplus is meant to fund growth;
        //       Settlement.Population is still read by nothing else.
        // TODO: Civilians work every tile at once, which a population cannot do. An allocation - a count per Settlement and tile, with a tile taking a share
        //       of its regrowth proportional to how fully it is staffed - replaces SettlementWorkAllocation and nothing else.
        // TODO: Rerun risk. The pipeline is not transactional, so if a later start-of-Round process fails and the Round is started again, every Settlement
        //       harvests a second time. Gathering carries the same risk; guard both once the pipeline's retry story is settled.

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Scalars only. The owner is the Structure's owner, which is where a Settlement's ownership lives and what a conquest moves, and a Settlement whose
        // Structure has been removed was razed along with its territory.
        var settlements = await db.Settlements
            .Where(settlement => settlement.GameId == context.GameId)
            .Where(settlement => settlement.StructureInstance.RemovedOnTurnId == null)
            .Select(settlement => new
            {
                settlement.Id,
                settlement.Population,
                OwnerPlayerId = settlement.StructureInstance.OwnedByPlayerId
            })
            .ToListAsync();

        if (settlements.Count == 0)
        {
            return string.Empty;
        }

        int[] settlementIds = [.. settlements.Select(settlement => settlement.Id)];

        // The territory. The coordinates come back because the rivers that enrich a tile are keyed by them rather than by tile id, and the three material
        // values because they are what the material half of the harvest takes.
        List<TerritoryTile> tiles = await db.CampaignMaps
            .Where(tile => tile.GameId == context.GameId)
            .Where(tile => tile.OwnedBySettlementId != null && settlementIds.Contains(tile.OwnedBySettlementId.Value))
            .Select(tile => new TerritoryTile(
                tile.CampaignMapId,
                tile.OwnedBySettlementId!.Value,
                tile.XCoord,
                tile.YCoord,
                tile.BiomeTypeId,
                tile.Biome.IsWater,
                tile.Geography.IsWater,
                tile.Biome.BaseWoodValue,
                tile.Biome.BaseStoneValue,
                tile.Biome.BaseBoneValue))
            .ToListAsync();

        if (tiles.Count == 0)
        {
            return string.Empty;
        }

        // Null when the Game's geometry could not be read, which is told apart from "no rivers" below: it must not be mistaken for a tile that lost one.
        IReadOnlyDictionary<Coord, TileRivers>? riversByCoord = await ReadRiversAsync(db, context.GameId, [.. tiles.Select(tile => new Coord(tile.XCoord, tile.YCoord))]);

        // Through the reader the end-of-Round regrowth also uses, so the cap is exactly what the Round will give back.
        Dictionary<BiomeType, BiomeRegenerationValues> biomesByType = await biomeRegenerationValueReader.GetValuesAsync(db);

        // One read per owning Player, however many Settlements they hold.
        var valuesByPlayerId = new Dictionary<int, PlayerSustenanceValues>();

        foreach (int playerId in settlements.Select(settlement => settlement.OwnerPlayerId).Distinct())
        {
            valuesByPlayerId[playerId] = await sustenanceValueReader.GetValuesAsync(db, playerId);
        }

        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds = await sustenanceAssetReader.GetAssetIdsAsync(db);
        IReadOnlyDictionary<MaterialResource, int> materialAssetIds = await materialAssetReader.GetAssetIdsAsync(db);

        int[] tileIds = [.. tiles.Select(tile => tile.CampaignMapId).Distinct()];

        // Tracked, because these rows are about to be written. A tile never taken from has no row yet, and is given one only if something is actually taken.
        Dictionary<int, CampaignMapDepletion> depletionByTileId = await db.CampaignMapDepletions
            .Where(depletion => tileIds.Contains(depletion.CampaignMapId))
            .ToDictionaryAsync(depletion => depletion.CampaignMapId);

        Dictionary<int, List<TerritoryTile>> tilesBySettlementId = tiles
            .GroupBy(tile => tile.SettlementId)
            .ToDictionary(group => group.Key, group => group.OrderBy(tile => tile.CampaignMapId).ToList());

        int harvestedSettlementCount = 0;
        ScaledAmount totalTaken = ScaledAmount.Zero;
        ScaledAmount totalUnplaced = ScaledAmount.Zero;

        foreach (var settlement in settlements.OrderBy(settlement => settlement.Id))
        {
            if (tilesBySettlementId.TryGetValue(settlement.Id, out List<TerritoryTile>? territory) == false)
            {
                // No territory at all, which is an unfinished Settlement: its ownership is not written until construction completes.
                continue;
            }

            long civilians = SettlementWorkAllocation.CiviliansOnTile(settlement.Population);
            PlayerSustenanceValues values = valuesByPlayerId[settlement.OwnerPlayerId];

            var takenByAssetId = new Dictionary<int, ScaledAmount>();
            var sustenanceTakes = new List<SustenanceTake>();

            foreach (TerritoryTile tile in territory)
            {
                bool isWater = CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater);
                TileRivers? rivers = riversByCoord?.GetValueOrDefault(new Coord(tile.XCoord, tile.YCoord));

                HarvestSustenance(tile, isWater, rivers, civilians, values, biomesByType, sustenanceAssetIds, depletionByTileId, takenByAssetId, sustenanceTakes);
                HarvestMaterials(tile, civilians, materialAssetIds, takenByAssetId);
            }

            AssetCredit[] credits =
            [
                .. takenByAssetId
                    .Where(entry => entry.Value > ScaledAmount.Zero)
                    .OrderBy(entry => entry.Key)
                    .Select(entry => AssetCredit.AtSettlement(settlement.OwnerPlayerId, settlement.Id, entry.Key, entry.Value))
            ];

            if (credits.Length == 0)
            {
                continue;
            }

            // One call per Settlement, so a Settlement with no bank cannot take the rest of the Game's harvest down with it. Capacity still binds correctly:
            // it binds per Settlement, and a later call picks up the rows an earlier one left in the change tracker.
            ServiceResult<IReadOnlyList<AssetCreditPlacement>> creditResult = await creditService.CreditAsync(db, credits);

            if (creditResult.HasSucceeded == false)
            {
                logger.LogWarning(
                    "Settlement Id {SettlementId} in Game Id {GameId} could not bank its harvest, so it took nothing from its territory this Round: {Errors}",
                    settlement.Id,
                    context.GameId,
                    string.Join("; ", creditResult.Errors));

                continue;
            }

            // Only now is the land charged. A Settlement that could not bank what it drew has not drawn it.
            foreach (SustenanceTake take in sustenanceTakes)
            {
                DepletionTaken.AddTaken(GetOrAddDepletion(db, depletionByTileId, take.CampaignMapId, context.GameId), take.Resource, take.Amount);
            }

            harvestedSettlementCount++;
            totalTaken = takenByAssetId.Values.Aggregate(totalTaken, (running, amount) => running + amount);

            if (creditResult.SuccessValue is not null)
            {
                totalUnplaced = creditResult.SuccessValue.Aggregate(totalUnplaced, (running, placement) => running + ScaledAmount.FromRaw(placement.RawUnplaced));
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Harvested {SettlementCount} Settlement(s) across {TileCount} territory tile(s) in Game Id {GameId} at the start of Round {RoundNumber}: "
                + "{TotalTaken} taken, {TotalUnplaced} of it wasted for want of storage.",
            harvestedSettlementCount,
            tiles.Count,
            context.GameId,
            context.CurrentRoundNumber,
            totalTaken.ToDecimal(),
            totalUnplaced.ToDecimal());

        return string.Empty;
    }

    /// <summary>
    /// Takes one tile's sustenance for a Settlement, accumulating what is banked and what the tile is to be charged.
    /// </summary>
    /// <remarks>
    /// The charge is accumulated rather than applied, because a Settlement that cannot bank its harvest must not have taken it off the land either.
    /// </remarks>
    private void HarvestSustenance(
        TerritoryTile tile,
        bool isWater,
        TileRivers? rivers,
        long civilians,
        PlayerSustenanceValues values,
        Dictionary<BiomeType, BiomeRegenerationValues> biomesByType,
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds,
        Dictionary<int, CampaignMapDepletion> depletionByTileId,
        Dictionary<int, ScaledAmount> takenByAssetId,
        List<SustenanceTake> sustenanceTakes)
    {
        if (biomesByType.TryGetValue(tile.BiomeTypeId, out BiomeRegenerationValues? biome) == false)
        {
            // A tile whose biome has no catalogue row is a data defect, but refusing here would stop the Round from starting at all.
            logger.LogWarning("Biome {BiomeType} has no catalogue row, so territory tile Id {CampaignMapTileId} was not harvested.", tile.BiomeTypeId, tile.CampaignMapId);

            return;
        }

        CampaignMapDepletion? depletion = depletionByTileId.GetValueOrDefault(tile.CampaignMapId);

        foreach (SustenanceResource resource in _SUSTENANCE_RESOURCES)
        {
            if (sustenanceAssetIds.TryGetValue(resource, out int assetId) == false)
            {
                // The catalogue has no Asset for this resource, so there is nothing to bank it as. A seeding fault, which SustenanceAssetReader documents.
                continue;
            }

            // What the owner can reach on the tile, once the river is counted and what anyone has already taken is removed.
            ScaledAmount? effectiveValue = RiverSustenanceBonus.Apply(values.GetValue(tile.BiomeTypeId, resource), resource, rivers, isWater);
            ScaledAmount? available = SustenanceValueCalculator.CalculateAvailableValue(effectiveValue, DepletionTaken.GetTaken(depletion, resource));

            // The cap: what the tile gives back at the end of this Round, against the same base the replenishment measures.
            ScaledAmount? regrowth = CalculateRegrowth(biome, resource, rivers, isWater);

            ScaledAmount take = SettlementHarvestCalculator.CalculateSustenanceTake(available, regrowth, civilians);

            if (take <= ScaledAmount.Zero)
            {
                continue;
            }

            takenByAssetId[assetId] = takenByAssetId.GetValueOrDefault(assetId) + take;
            sustenanceTakes.Add(new SustenanceTake(tile.CampaignMapId, resource, take));
        }
    }

    /// <summary>
    /// Takes one tile's materials for a Settlement, accumulating what is banked.
    /// </summary>
    /// <remarks>
    /// Nothing is charged to the tile and nothing is subtracted for what has been taken before, because a material value is static: the same amount is there
    /// next Round. That is also why there is no river bonus and no Player modifier here - neither applies to a material.
    /// </remarks>
    private static void HarvestMaterials(
        TerritoryTile tile,
        long civilians,
        IReadOnlyDictionary<MaterialResource, int> materialAssetIds,
        Dictionary<int, ScaledAmount> takenByAssetId)
    {
        foreach (MaterialResource resource in _MATERIAL_RESOURCES)
        {
            if (materialAssetIds.TryGetValue(resource, out int assetId) == false)
            {
                // The catalogue has no Asset for this material, so there is nothing to bank it as. A seeding fault, which IMaterialAssetReader documents.
                continue;
            }

            ScaledAmount take = SettlementHarvestCalculator.CalculateMaterialTake(tile.GetMaterialBaseValue(resource), civilians);

            if (take <= ScaledAmount.Zero)
            {
                continue;
            }

            takenByAssetId[assetId] = takenByAssetId.GetValueOrDefault(assetId) + take;
        }
    }

    /// <summary>
    /// What the tile regrows of one resource at the end of this Round, or <see langword="null"/> where it regrows nothing.
    /// </summary>
    /// <remarks>
    /// A biome with no Regeneration Percent has no regrowth to cap a harvest at, so nothing is taken from it rather than the whole standing stock. That is
    /// data saved before the biome validator required one; re-seeding fixes it.
    /// </remarks>
    private static ScaledAmount? CalculateRegrowth(BiomeRegenerationValues biome, SustenanceResource resource, TileRivers? rivers, bool isWater)
    {
        if (biome.RegenerationPercent is null)
        {
            return null;
        }

        // The tile when full: the biome's base, plus whatever its rivers add - the same base the end-of-Round regrowth measures against.
        ScaledAmount? baseValue = RiverSustenanceBonus.Apply(biome.GetBaseValue(resource), resource, rivers, isWater);

        return baseValue is null ? null : TileRegenerationCalculator.CalculateRegrowth(baseValue.Value, biome.RegenerationPercent.Value);
    }

    /// <summary>
    /// The tile's depletion row, adding one where the tile has never been taken from.
    /// </summary>
    private static CampaignMapDepletion GetOrAddDepletion(
        AscendantEmpireContext db,
        Dictionary<int, CampaignMapDepletion> depletionByTileId,
        int campaignMapId,
        int gameId)
    {
        if (depletionByTileId.TryGetValue(campaignMapId, out CampaignMapDepletion? depletion))
        {
            return depletion;
        }

        depletion = new CampaignMapDepletion { CampaignMapId = campaignMapId, GameId = gameId };

        db.CampaignMapDepletions.Add(depletion);
        depletionByTileId.Add(campaignMapId, depletion);

        return depletion;
    }

    /// <summary>
    /// The river edges of the territory tiles, keyed by coordinate, or <see langword="null"/> when they could not be read.
    /// </summary>
    /// <remarks>
    /// A Game whose geometry cannot be read is a data defect, but refusing here would stop the Round from starting. The Settlements harvest off their biomes
    /// alone instead, which is what every tile offered before rivers enriched them.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Coord, TileRivers>?> ReadRiversAsync(AscendantEmpireContext db, int gameId, IReadOnlyCollection<Coord> coords)
    {
        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(db, gameId);

        if (geometry is null)
        {
            logger.LogWarning("Game Id {GameId}'s map geometry was not found while harvesting Settlement territory, so no river yield was counted on any tile.", gameId);

            return null;
        }

        return await campaignRiverReader.GetTileRiversAsync(db, gameId, coords, geometry);
    }

    /// <summary>
    /// The ids of the tiles being harvested, for the depletion read.
    /// </summary>
    private static int[] depletionTileIds(List<TerritoryTile> tiles)
    {
        return [.. tiles.Select(tile => tile.CampaignMapId).Distinct()];
    }

    /// <summary>
    /// One tile of one Settlement's territory: where it is, what it is, and what materials its biome holds.
    /// </summary>
    /// <remarks>
    /// The material base values ride along on the tile because they are read off the biome in the same query and are needed per tile; the sustenance values
    /// do not, because they are the owning Player's rather than the biome's and are read once per Player.
    /// </remarks>
    private sealed record TerritoryTile(
        int CampaignMapId,
        int SettlementId,
        int XCoord,
        int YCoord,
        BiomeType BiomeTypeId,
        bool BiomeIsWater,
        bool GeographyIsWater,
        long? BaseWoodValue,
        long? BaseStoneValue,
        long? BaseBoneValue)
    {
        /// <summary>
        /// The biome base value column that holds the given material, or <see langword="null"/> where the biome does not have it.
        /// </summary>
        public ScaledAmount? GetMaterialBaseValue(MaterialResource resource)
        {
            long? rawValue = resource switch
            {
                MaterialResource.Wood => BaseWoodValue,
                MaterialResource.Stone => BaseStoneValue,
                MaterialResource.Bone => BaseBoneValue,
                _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Not a material resource.")
            };

            return rawValue is null ? null : ScaledAmount.FromRaw(rawValue.Value);
        }
    }

    /// <summary>
    /// One amount of one sustenance resource a Settlement is to be charged for, once its harvest is safely banked.
    /// </summary>
    private sealed record SustenanceTake(int CampaignMapId, SustenanceResource Resource, ScaledAmount Amount);
}
