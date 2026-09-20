using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>map.CampaignGeographies</c> catalogue.
/// </summary>
/// <remarks>
/// Stands alone: a geography references nothing else the seed writes, and nothing the seed writes references one.
/// </remarks>
internal sealed class GeographySeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Geographies;

    public SeedTargetGroup Group => SeedTargetGroup.Map;

    public string DisplayName => "Geographies";

    public string Description => "The elevation and movement half of a tile's terrain — flat, hilly, mountainous, deep and shallow water.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 10;

    public int ItemCount => GeographySeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertCampaignMapGeographiesRequest(GeographySeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "geographies", request, cancellationToken);

        return $"{ItemCount} geography(ies)";
    }
}
