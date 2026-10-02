using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;
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
/// </remarks>
internal class RegenerateTileSustenance(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IBiomeRegenerationValueReader biomeRegenerationValueReader,
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

        // Scalars only: the biome is all that is needed off each tile.
        Dictionary<int, BiomeType> biomeTypeByTileId = await db.CampaignMaps
            .Where(t => tileIds.Contains(t.CampaignMapId))
            .Select(t => new { t.CampaignMapId, t.BiomeTypeId })
            .ToDictionaryAsync(t => t.CampaignMapId, t => t.BiomeTypeId);

        // Through the reader the support forecast also uses, so the forecast regrows a tile by exactly what this process will.
        Dictionary<BiomeType, BiomeRegenerationValues> biomesByType = await biomeRegenerationValueReader.GetValuesAsync(db);

        int regeneratedTileCount = 0;

        foreach (CampaignMapDepletion depletion in depletions.OrderBy(d => d.CampaignMapId))
        {
            if (biomeTypeByTileId.TryGetValue(depletion.CampaignMapId, out BiomeType biomeType) == false || biomesByType.TryGetValue(biomeType, out BiomeRegenerationValues? biome) == false)
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
                    biomeType,
                    depletion.CampaignMapId,
                    context.GameId);

                continue;
            }

            // The marker records the last Round anyone fed here, so a match means this Round.
            bool fedOn = depletion.FedOnRound == context.CurrentRoundNumber;

            foreach (SustenanceResource resource in _SUSTENANCE_RESOURCES)
            {
                long? baseRawValue = biome.GetBaseValue(resource);

                // A resource the biome does not have is never regenerated.
                if (baseRawValue is null)
                {
                    continue;
                }

                ScaledAmount newTaken = TileRegenerationCalculator.Regenerate(
                    DepletionTaken.GetTaken(depletion, resource),
                    ScaledAmount.FromRaw(baseRawValue.Value),
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
}
