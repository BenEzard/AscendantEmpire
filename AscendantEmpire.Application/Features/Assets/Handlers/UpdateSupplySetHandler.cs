using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

/// <summary>
/// Updates one supply set from the dev Entity Setup Tool.
/// </summary>
/// <remarks>
/// <para>
/// The payload is the whole intended state: members are reconciled to exactly what it carries, so one left out is removed. A member the payload still
/// describes keeps its row, and with it anything referencing that row.
/// </para>
/// <para>
/// The type is the one field that freezes once components require the set. A requirement names a set and a quantity, and the set's type is what that quantity
/// is measured against — per troop, per movement point, per damage point. Changing it would leave every requirement asking for the same number against a
/// different unit, with nothing to show that the meaning moved. The name, the code and the members stay editable, since none of them changes what a
/// requirement is asking for.
/// </para>
/// </remarks>
public record UpdateSupplySetRequest(int Id, UpsertSupplySetDto Set) : IRequest<ServiceResult<SupplySetView>>;

internal class UpdateSupplySetHandler(
    ILogger<UpdateSupplySetHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<UpdateSupplySetRequest, ServiceResult<SupplySetView>>
{
    public async Task<ServiceResult<SupplySetView>> Handle(UpdateSupplySetRequest request, CancellationToken cancelToken)
    {
        UpsertSupplySetDto dto = request.Set;

        List<string> errors = SupplySetValidation.Validate(dto);

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpdateSupplySetRequest), string.Join("; ", errors));

            return ServiceResult<SupplySetView>.Fail(errors);
        }

        SupplySet? set = await context.SupplySets
            .Include(existing => existing.Assets)
            .SingleOrDefaultAsync(existing => existing.Id == request.Id, cancelToken);

        if (set is null)
        {
            return ServiceResult<SupplySetView>.NotFound($"No supply set exists with id {request.Id}.");
        }

        if (set.SupplyType != dto.SupplyType)
        {
            int requiredByCount = await context.UnitComponentSupplyRequirements
                .CountAsync(requirement => requirement.SupplySetId == request.Id, cancelToken);

            if (requiredByCount > 0)
            {
                string components = requiredByCount == 1 ? "component" : "components";

                return ServiceResult<SupplySetView>.Conflict(
                    $"Supply set '{set.Code}' is required by {requiredByCount} {components}, so its type cannot be changed from {set.SupplyType} to " +
                    $"{dto.SupplyType}. Author a separate set for the other need instead.");
            }
        }

        bool codeTakenByAnother = await context.SupplySets
            .AnyAsync(other => other.Id != request.Id && other.Code == dto.Code, cancelToken);

        if (codeTakenByAnother)
        {
            return ServiceResult<SupplySetView>.Conflict($"Another supply set already uses code '{dto.Code}'.");
        }

        (Dictionary<string, int> assetIdsByCode, string[] unresolvedCodes) = await SupplySetProjections.ResolveAssetsAsync(context, dto.Assets, cancelToken);

        if (unresolvedCodes.Length > 0)
        {
            return ServiceResult<SupplySetView>.Fail(SupplySetProjections.DescribeUnresolved(unresolvedCodes));
        }

        set.Code = dto.Code;
        set.DisplayName = dto.DisplayName;
        set.SupplyType = dto.SupplyType;

        ReconcileMembers(set, dto.Assets, assetIdsByCode);

        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Updated supply set '{Code}' (id {Id}) to {MemberCount} members.", set.Code, set.Id, dto.Assets.Count);

        SupplySet saved = await context.SupplySets
            .Include(existing => existing.Assets)
            .ThenInclude(member => member.Asset)
            .SingleAsync(existing => existing.Id == request.Id, cancelToken);

        return ServiceResult<SupplySetView>.Succeed(SupplySetProjections.ToView(saved));
    }

    /// <summary>
    /// Brings the set's members in line with the payload, keeping the rows it still describes.
    /// </summary>
    private void ReconcileMembers(SupplySet set, IReadOnlyCollection<UpsertSupplySetAssetDto> members, Dictionary<string, int> assetIdsByCode)
    {
        Dictionary<int, UpsertSupplySetAssetDto> wantedByAssetId = members.ToDictionary(member => assetIdsByCode[member.AssetCode]);

        SupplySetAsset[] removed = [.. set.Assets.Where(member => !wantedByAssetId.ContainsKey(member.AssetId))];

        context.SupplySetAssets.RemoveRange(removed);

        foreach (SupplySetAsset member in removed)
        {
            set.Assets.Remove(member);
        }

        foreach ((int assetId, UpsertSupplySetAssetDto wanted) in wantedByAssetId)
        {
            SupplySetAsset? existing = set.Assets.SingleOrDefault(member => member.AssetId == assetId);

            if (existing is null)
            {
                set.Assets.Add(new SupplySetAsset
                {
                    AssetId = assetId,
                    AmountPerUnit = wanted.AmountPerUnit,
                    SelectionWeight = wanted.SelectionWeight
                });

                continue;
            }

            existing.AmountPerUnit = wanted.AmountPerUnit;
            existing.SelectionWeight = wanted.SelectionWeight;
        }
    }
}
