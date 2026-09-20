using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>map.MapResourceCategory</c> rows the map editor groups resource types under.
/// </summary>
/// <remarks>
/// Categories only, so the step stands alone. MapResourceTypes are not seeded because their yields name <c>resources.Resource</c> rows this seed does not
/// create — see <see cref="MapResourceCategorySeedData"/>. Were they ever added, that step would depend on both this one and biomes.
/// </remarks>
internal sealed class MapResourceCategorySeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.MapResourceCategories;

    public SeedTargetGroup Group => SeedTargetGroup.Map;

    public string DisplayName => "Map Resource Categories";

    public string Description => "The groups the MapResource Type Manager lists resource types under — food, animals, wood, stone, metals, fuel, luxuries.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 30;

    public int ItemCount => MapResourceCategorySeedData.Build().Categories.Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new PutMapResourceCatalogueRequest(MapResourceCategorySeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "map resource categories", request, cancellationToken);

        return $"{ItemCount} map resource category(ies)";
    }
}
