using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;

/// <summary>
/// Regrows the sustenance on every depleted campaign map tile in the Game at the end of the Round, reducing each resource's Player-neutral <c>Taken</c> amount.
/// </summary>
/// <remarks>
/// <para>
/// Runs once per tile, for each sustenance resource the tile's biome has (a non-null base), using the rules in <see cref="TileRegenerationCalculator"/>. See "Replenishment"
/// in <c>docs/design/campaign-map/replenishment.md</c>.
/// </para>
/// <para>
/// Only tiles with something taken are read: a tile whose every <c>Taken</c> is 0 has nothing to regrow, whether or not it was fed on. A row that regrows back to 0 is kept, and
/// reused the next time a Force feeds there.
/// </para>
/// <para>
/// A tile was fed on this Round when <see cref="CampaignMapDepletion.FedOnRound"/> is the Round ending, so there is no marker to clear for the next Round.
/// </para>
/// <para>
/// Every tile is treated as unclaimed for now: <c>Base</c> is the biome's base value and the share that regrows is the biome's Regeneration Percent.
/// </para>
/// <para>
/// A land tile's rivers are part of that base, through <see cref="RiverSustenanceBonus"/>: a riverbank holds more Hunt and Fodder than its biome alone, and holds Fish its
/// biome does not have at all, so it has to regrow both. Without this a Force would eat a riverbank's Fish once and the tile would never give it back.
/// </para>
/// </remarks>
internal class RegenerateTileSustenance(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IBiomeRegenerationValueReader biomeRegenerationValueReader,
    ISightDataReader sightDataReader,
    ICampaignRiverReader campaignRiverReader,
    ILogger<RegenerateTileSustenance> logger) : IEndCampaignRoundProcess
{
    /// <summary>
    /// Every sustenance resource, in declaration order.
    /// </summary>
    private static readonly SustenanceResource[] _SUSTENANCE_RESOURCES = Enum.GetValues<SustenanceResource>();

    public string ItemName => "Regenerate Tile Sustenance";

    public int ExecutionOrder => (int)EndCampaignRoundProcessOrder.RegenerateTileSustenance;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: A claimed tile (within a Player's territory) should regrow against the claimant's effective values (IPlayerSustenanceValueReader) as Base, and the biome's
        //       Regeneration Percent plus the claimant's Settlement-scoped regeneration modifiers (in percentage points). Waits on Settlements being modelled. The support forecast
        //       (TileSupportForecaster) regrows through the same values and must follow - see its TODO on revealing a claimant's tech.
        // TODO: Tiles within a Settlement zone also need to be found for replenishment. Waits on Settlements.
        // TODO: Whether a Settlement's population feeding on its own territory counts as feeding on the tile is deferred to the Settlement mechanics.
        // TODO: Scorched earth setting Taken is undecided (see docs/design/campaign-map/depletion.md).
        // TODO: Rerun risk. The pipeline is not transactional, so if a later end-of-Round process fails and the Round is ended again, these tiles regrow a second time.
        //       Guard it (a per-Round regenerated stamp) once the pipeline's retry story is settled.

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Tracked, because these rows are about to be written. Rows with nothing taken are left out: there is nothing on them to regrow.
        List<CampaignMapDepletion> depletions = await db.CampaignMapDepletions
            .Where(d => d.GameId == context.GameId)
            .Where(d => d.ForageTaken > 0 || d.HuntTaken > 0 || d.FishTaken > 0 || d.FodderTaken > 0)
            .ToListAsync();

        if (depletions.Count == 0)
        {
            return string.Empty;
        }

        int[] tileIds = [.. depletions.Select(d => d.CampaignMapId)];

        // Scalars only. The coordinates and the water facts come back alongside the biome, because a tile's rivers are part of what it regrows against and they are keyed
        // by coordinate rather than by tile id.
        var tiles = await db.CampaignMaps
            .Where(t => tileIds.Contains(t.CampaignMapId))
            .Select(t => new { t.CampaignMapId, t.XCoord, t.YCoord, t.BiomeTypeId, BiomeIsWater = t.Biome.IsWater, GeographyIsWater = t.Geography.IsWater })
            .ToDictionaryAsync(t => t.CampaignMapId);

        // The rivers on the depleted tiles, in one call. A riverbank regrows the Fish the river put there, so the base it regrows against has to carry the river too.
        // Null when the Game's geometry could not be read, which is told apart from "no rivers" below: it must not be mistaken for a tile that lost one.
        IReadOnlyDictionary<Coord, TileRivers>? riversByCoord = await ReadRiversAsync(db, context.GameId, tiles.Values.Select(t => new Coord(t.XCoord, t.YCoord)).ToList());

        // Through the reader the support forecast also uses, so the forecast regrows a tile by exactly what this process will.
        Dictionary<BiomeType, BiomeRegenerationValues> biomesByType = await biomeRegenerationValueReader.GetValuesAsync(db);

        int regeneratedTileCount = 0;

        foreach (CampaignMapDepletion depletion in depletions.OrderBy(d => d.CampaignMapId))
        {
            if (tiles.TryGetValue(depletion.CampaignMapId, out var tile) == false || biomesByType.TryGetValue(tile.BiomeTypeId, out BiomeRegenerationValues? biome) == false)
            {
                // A depletion row for a tile or biome that does not exist is a data defect, but failing here would stop the Round from ending at all.
                logger.LogWarning(
                    "The campaign map tile or biome for depletion on tile Id {CampaignMapTileId} was not found in Game Id {GameId}, so the tile did not regenerate.",
                    depletion.CampaignMapId,
                    context.GameId);

                continue;
            }

            if (biome.RegenerationPercent is null)
            {
                // Something was taken, so the biome has a sustenance base, and the biome validator requires a Regeneration Percent wherever there is one. This is data saved before that
                // rule existed; re-seeding fixes it.
                logger.LogWarning(
                    "Biome {BiomeType} has no Regeneration Percent, so depleted tile Id {CampaignMapTileId} in Game Id {GameId} did not regenerate.",
                    tile.BiomeTypeId,
                    depletion.CampaignMapId,
                    context.GameId);

                continue;
            }

            // The marker records the last Round anyone fed here, so a match means this Round.
            bool fedOn = depletion.FedOnRound == context.CurrentRoundNumber;

            bool isWater = CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater);
            TileRivers? rivers = riversByCoord?.GetValueOrDefault(new Coord(tile.XCoord, tile.YCoord));

            foreach (SustenanceResource resource in _SUSTENANCE_RESOURCES)
            {
                // What the tile holds when full: the biome's base, plus whatever its rivers add. A riverbank regrows the Fish a river put on it, which the biome alone
                // would never give back.
                ScaledAmount? baseValue = RiverSustenanceBonus.Apply(biome.GetBaseValue(resource), resource, rivers, isWater);

                if (baseValue is null)
                {
                    // A resource the tile does not have is never regenerated. Anything standing against it was taken while the tile still had it - a river since removed
                    // from the map - and is cleared rather than left to be subtracted if the river is ever drawn back. Only where the rivers are actually known: a
                    // geometry read that failed must not be read as a river that went away.
                    if (riversByCoord is not null)
                    {
                        DepletionTaken.SetTaken(depletion, resource, ScaledAmount.Zero);
                    }

                    continue;
                }

                ScaledAmount newTaken = TileRegenerationCalculator.Regenerate(
                    DepletionTaken.GetTaken(depletion, resource),
                    baseValue.Value,
                    biome.RegenerationPercent.Value,
                    fedOn);

                DepletionTaken.SetTaken(depletion, resource, newTaken);
            }

            regeneratedTileCount++;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Regenerated {TileCount} depleted tile(s) in Game Id {GameId} at the end of Round {RoundNumber}.",
            regeneratedTileCount,
            context.GameId,
            context.CurrentRoundNumber);

        return string.Empty;
    }

    /// <summary>
    /// The river edges of the depleted tiles, keyed by coordinate, or <see langword="null"/> when they could not be read.
    /// </summary>
    /// <remarks>
    /// A Game whose geometry cannot be read is a data defect, but refusing here would stop the Round from ending at all. The tiles regrow against their biomes alone
    /// instead, which leaves a riverbank's Fish to be picked up by the next Round's regrowth. The null is what tells that apart from a tile that truly has no river.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Coord, TileRivers>?> ReadRiversAsync(AscendantEmpireContext db, int gameId, IReadOnlyCollection<Coord> coords)
    {
        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(db, gameId);

        if (geometry is null)
        {
            logger.LogWarning("Game Id {GameId}'s map geometry was not found while regenerating tiles, so no river food was regrown on any of them.", gameId);

            return null;
        }

        return await campaignRiverReader.GetTileRiversAsync(db, gameId, coords, geometry);
    }
}
