using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;

/// <summary>
/// Regenerates a Game's renewable MapResources at the end of a Round: spread, then growth, then regrowth.
/// </summary>
/// <remarks>
/// <para>
/// Every renewable instance is classified once, from the state as read, into exactly one step:
/// <list type="bullet">
///   <item><description><b>Spread</b>: at or above its ceiling. One that cannot spread does nothing, and does not grow either.</description></item>
///   <item><description><b>Growth</b>: above 0 and below its ceiling, including any that spread topped up this Round.</description></item>
///   <item><description><b>Regrowth</b>: at 0, and only if still at 0 once spread has run, since spread can revive it.</description></item>
/// </list>
/// A Distribution is classified by the total of its amounts. Instances spread creates or revives are not classified, so they wait for the next Round.
/// </para>
/// <para>
/// The steps work on the candidates in memory, and the result is written once, in one transaction. See the End of round order in
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// </remarks>
internal class RegenerateMapResources(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMapResourceRegenerationReader reader,
    IMapResourceRegenerationWriter writer,
    ISightDataReader sightReader,
    ILogger<RegenerateMapResources> logger
    ) : IEndCampaignRoundProcess
{
    public string ItemName => "Regenerate Map Resources";

    public int ExecutionOrder => (int)EndCampaignRoundProcessOrder.RegenerateMapResources;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: An owned tile should regenerate against its owner's modifiers (PlayerMapResourceModifier). Every tile uses its habitat's values for now.
        // TODO: Rerun risk, as in RegenerateTileSustenance: the pipeline is not transactional, so a Round ended again grows every instance a second time.

        await using AscendantEmpireContext dbContext = await dbFactory.CreateDbContextAsync();

        long? gameSeed = await reader.GetGameSeedAsync(dbContext, context.GameId);

        if (gameSeed is not long seed)
        {
            // Rolling against an invented seed would make spread and regrowth unreproducible.
            logger.LogError("{ItemName} could not find Game Id {GameId}, so no MapResources were regenerated.", ItemName, context.GameId);
            return $"{ItemName} could not find Game Id {context.GameId}, so no MapResources were regenerated.";
        }

        CampaignMapGeometry? mapGeometry = await sightReader.GetGeometryAsync(dbContext, context.GameId);

        if (mapGeometry is null)
        {
            logger.LogError("{ItemName} could not find the CampaignMap for Game Id {GameId}, so no MapResources were regenerated.", ItemName, context.GameId);
            return $"{ItemName} could not find the CampaignMap for Game Id {context.GameId}, so no MapResources were regenerated.";
        }

        IReadOnlyCollection<RenewableHabitat> renewableHabitats = await reader.GetRenewableHabitatConfigAsync(dbContext);
        IReadOnlyCollection<MapResourceRegenerationInstance> instances = await reader.GetMapResourceInstancesAsync(dbContext, context.GameId);

        Dictionary<int, Dictionary<HabitatScope, RenewableHabitat>> habitatsByResourceType = renewableHabitats
            .GroupBy(habitat => habitat.MapResourceTypeId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(habitat => new HabitatScope(habitat.BiomeTypeId, habitat.CampaignMapGeographyTypeId)));

        List<MapResourceRegenerationCandidate> candidates = [];

        foreach (MapResourceRegenerationInstance instance in instances)
        {
            RenewableHabitat? habitat = MapResourceRegenerationRules.FindHabitat(habitatsByResourceType, instance.MapResourceTypeId, instance.BiomeTypeId, instance.CampaignMapGeographyTypeId);

            // The tile no longer suits the type, so the instance is left as it is.
            if (habitat == null)
            {
                continue;
            }

            candidates.Add(new MapResourceRegenerationCandidate(instance, habitat, MapResourceRegenerationRules.Classify(instance, habitat)));
        }

        // Spread
        SpreadTargetResults spreadTargetResults = MapResourceSpreadRules.SelectSpreadTargets(
            mapGeometry,
            candidates: [.. candidates.Where(candidate => candidate.RegenerationType == RegenerationCandidateType.Spread).OrderBy(candidate => candidate.Instance.MapResourceInstanceId)],
            gameSeed: seed,
            currentRoundNumber: context.CurrentRoundNumber);

        IReadOnlyDictionary<int, SpreadTargetTile> spreadTargets = await reader.GetSpreadTargetsAsync(dbContext, context.GameId, spreadTargetResults.TargetCampaignMapIds);

        MapResourceSpreadPlan spreadPlan = MapResourceSpreadRules.Plan(spreadTargetResults.SpreadIntent, spreadTargets, habitatsByResourceType, seed);

        MapResourceRegenerationRules.ApplySpreadPlan(candidates.ToDictionary(candidate => candidate.Instance.MapResourceInstanceId), spreadPlan);

        // Growth
        foreach (MapResourceRegenerationCandidate candidate in candidates.Where(candidate => candidate.RegenerationType == RegenerationCandidateType.Grow))
        {
            MapResourceRegenerationRules.ApplyGrowth(candidate);
        }

        // Regrowth
        foreach (MapResourceRegenerationCandidate candidate in candidates.Where(candidate => candidate is { RegenerationType: RegenerationCandidateType.Revive, IsRevivedBySpread: false }))
        {
            MapResourceRegenerationRules.ApplyRegrowth(candidate, seed, context.CurrentRoundNumber);
        }

        MapResourceRegenerationPlan plan = MapResourceRegenerationRules.BuildPlan(candidates, spreadPlan);

        await writer.WriteAsync(dbContext, context.GameId, context.CurrentRoundNumber, plan);

        logger.LogInformation(
            "Regenerated MapResources in Game Id {GameId} at the end of Round {RoundNumber}: {SpreadCount} spread, {CreatedCount} created, {UpdatedCount} updated, {DeletedCount} deleted.",
            context.GameId,
            context.CurrentRoundNumber,
            spreadPlan.SpreadsUsed.Values.Sum(),
            plan.Creations.Count,
            plan.InstanceUpdates.Count,
            plan.DeletedMapResourceInstanceIds.Count);

        return string.Empty;
    }
}
