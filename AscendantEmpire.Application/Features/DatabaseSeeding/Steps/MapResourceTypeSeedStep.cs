using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>map.MapResourceType</c> rows and their children - every MapResource a tile can hold, which today means Timber.
/// </summary>
/// <remarks>
/// <para>
/// Follows three other targets. A type has to have a category to be listed under, the biome catalogue has to exist because a habitat and a Distribution
/// scenario weight both point at a <c>map.CampaignBiome</c> row, and the Asset catalogue has to exist because a type's yields, scenarios and layers all name
/// the Assets extracting it gives.
/// </para>
/// <para>
/// Dispatches <see cref="SeedMapResourceTypesRequest"/> rather than the catalogue write directly, because the Asset ids a scenario needs can only be looked
/// up against a database and a step is handed nothing but a mediator.
/// </para>
/// </remarks>
internal sealed class MapResourceTypeSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.MapResourceTypes;

    public SeedTargetGroup Group => SeedTargetGroup.Map;

    public string DisplayName => "Map Resources";

    public string Description => "Every MapResource a tile can hold, with where it exists and what it is made of — Timber for now, and whatever else is written next.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.MapResourceCategories, SeedTarget.Biomes, SeedTarget.Assets];

    public int SortOrder => 40;

    public int ItemCount => MapResourceTypeSeedData.ItemCount;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new SeedMapResourceTypesRequest();

        await SeedStepDispatch.SendAsync(mediator, "map resources", request, cancellationToken);

        return $"{MapResourceTypeSeedData.ResourceCount} map resource(s)";
    }
}
