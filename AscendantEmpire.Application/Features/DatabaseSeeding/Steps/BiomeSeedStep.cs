using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>map.CampaignBiomes</c> catalogue.
/// </summary>
/// <remarks>
/// Stands alone. The resource values in the seed data are unscaled whole numbers — the upsert handler scales them by 1,000 as it writes, so this step must
/// not pre-scale them.
/// </remarks>
internal sealed class BiomeSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Biomes;

    public SeedTargetGroup Group => SeedTargetGroup.Map;

    public string DisplayName => "Biomes";

    public string Description => "The cover and resource half of a tile's terrain — grassland, forest, desert, ocean and the rest, with their base yields.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 20;

    public int ItemCount => BiomeSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertCampaignMapBiomesRequest(BiomeSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "biomes", request, cancellationToken);

        return $"{ItemCount} biome(s)";
    }
}
