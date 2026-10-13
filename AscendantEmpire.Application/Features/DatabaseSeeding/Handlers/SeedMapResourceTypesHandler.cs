using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;

/// <summary>
/// Seeds the MapResourceTypes in <see cref="MapResourceTypeSeedData"/>, resolving the Asset codes they name to ids first.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a gap between two contracts. A MapResourceType names its Assets by integer id, but the seed data can only know them by code - an
/// id is a database identity that differs between databases. A seed step cannot do the resolving itself: it is handed nothing but an <c>IMediator</c>, and
/// <c>SeedStepCatalogueTests</c> builds every step with no constructor arguments. So the lookup happens here, where there is a context to do it with, and
/// the finished payload goes on to <see cref="PutMapResourceCatalogueRequest"/> like any other catalogue write.
/// </para>
/// <para>
/// A missing Asset is reported by code rather than left to fail as "there is no Asset 0", since an unseeded Asset catalogue is by far the likeliest cause.
/// </para>
/// </remarks>
public record SeedMapResourceTypesRequest() : IRequest<ServiceResultBase>;

internal class SeedMapResourceTypesHandler(
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<SeedMapResourceTypesRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SeedMapResourceTypesRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> assetCodes = MapResourceTypeSeedData.AssetCodes;

        Dictionary<string, int> assetIdsByCode = await context.Assets
            .AsNoTracking()
            .Where(asset => assetCodes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, asset => asset.Id, cancellationToken);

        string[] missingAssetCodes = [.. assetCodes.Except(assetIdsByCode.Keys, StringComparer.OrdinalIgnoreCase)];

        if (missingAssetCodes.Length > 0)
        {
            return Fail($"Asset(s) not found in the catalogue: {string.Join(", ", missingAssetCodes)}. Run the Assets database seed from the Entity Setup Tool first.");
        }

        PutMapResourceCatalogueDto catalogue = MapResourceTypeSeedData.Build(assetIdsByCode);

        return await mediator.Send(new PutMapResourceCatalogueRequest(catalogue), cancellationToken);
    }
}
