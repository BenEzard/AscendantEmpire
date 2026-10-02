using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>assets</c> schema catalogue — the meta-categories, categories, groups and Assets themselves.
/// </summary>
/// <remarks>
/// Stands alone: the catalogue references nothing else, and it is what the MapResource Type Manager's yields and scenarios point at rather than the other way
/// round. Runs through the same upsert the Entity Setup Tool's editor does, so seeding and hand-editing cannot diverge in how a row is matched or written.
/// </remarks>
internal sealed class AssetSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Assets;

    public SeedTargetGroup Group => SeedTargetGroup.Assets;

    public string DisplayName => "Resources";

    public string Description => "The resource catalogue — construction materials, weapons and raw materials, with their categories and groups.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 10;

    public int ItemCount => CountAssets(AssetSeedData.Build());

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertAssetsRequest(AssetSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "assets", request, cancellationToken);

        return $"{ItemCount} resource(s)";
    }

    /// <summary>
    /// Counts the Assets in the catalogue, down both the grouped and the standalone path.
    /// </summary>
    /// <remarks>
    /// Counted off the seed data rather than written down, so the number shown beside the checkbox cannot drift from what the step actually writes. Only
    /// Assets are counted - the meta-categories, categories and groups are scaffolding for them rather than items in their own right.
    /// </remarks>
    private static int CountAssets(IReadOnlyCollection<UpsertAssetMetaCategoryDto> metaCategories)
    {
        return metaCategories
            .SelectMany(metaCategory => metaCategory.Categories)
            .Sum(category => category.CategoryType == AssetCategoryType.GroupedAssets
                ? category.Groups.Sum(group => group.Assets.Count)
                : category.Assets.Count);
    }
}
