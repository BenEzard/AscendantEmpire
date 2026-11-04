using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>assets.StorageClass</c> catalogue — how each Asset is kept, which capacity binds against.
/// </summary>
/// <remarks>
/// Stands alone: the catalogue references nothing else, and it is what the Assets point at rather than the other way round. Must run before
/// <see cref="AssetSeedStep"/>, which names these codes against a non-null FK — an unseeded class table fails every Asset insert at the database.
/// </remarks>
internal sealed class StorageClassSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.StorageClasses;

    public SeedTargetGroup Group => SeedTargetGroup.Assets;

    public string DisplayName => "Storage Classes";

    public string Description => "How each Asset is kept — the storage yard, the armoury, the granary and the rest, which capacity binds against.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    // Ahead of AssetSeedStep's 10, so a full seed lists and runs the classes before the Assets that name them.
    public int SortOrder => 5;

    public int ItemCount => StorageClassSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertStorageClassesRequest(StorageClassSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "storage classes", request, cancellationToken);

        return $"{ItemCount} storage class(es)";
    }
}
