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
/// Creates one supply set from the dev Entity Setup Tool.
/// </summary>
/// <remarks>
/// <see cref="SupplySet.Code"/> is the unique natural key, so a request whose code already exists is a conflict rather than a silent update — the update
/// endpoint is how an existing set changes.
/// </remarks>
public record CreateSupplySetRequest(UpsertSupplySetDto Set) : IRequest<ServiceResult<SupplySetView>>;

internal class CreateSupplySetHandler(
    ILogger<CreateSupplySetHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<CreateSupplySetRequest, ServiceResult<SupplySetView>>
{
    public async Task<ServiceResult<SupplySetView>> Handle(CreateSupplySetRequest request, CancellationToken cancelToken)
    {
        UpsertSupplySetDto dto = request.Set;

        // Shape validation first, so a malformed request never touches the database.
        List<string> errors = SupplySetValidation.Validate(dto);

        if (errors.Count > 0)
        {
            logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(CreateSupplySetRequest), string.Join("; ", errors));

            return ServiceResult<SupplySetView>.Fail(errors);
        }

        bool codeExists = await context.SupplySets.AnyAsync(set => set.Code == dto.Code, cancelToken);

        if (codeExists)
        {
            return ServiceResult<SupplySetView>.Conflict($"A supply set with code '{dto.Code}' already exists.");
        }

        (Dictionary<string, int> assetIdsByCode, string[] unresolvedCodes) = await SupplySetProjections.ResolveAssetsAsync(context, dto.Assets, cancelToken);

        if (unresolvedCodes.Length > 0)
        {
            return ServiceResult<SupplySetView>.Fail(SupplySetProjections.DescribeUnresolved(unresolvedCodes));
        }

        var set = new SupplySet
        {
            Code = dto.Code,
            DisplayName = dto.DisplayName,
            SupplyType = dto.SupplyType,
            Assets =
            [
                .. dto.Assets.Select(member => new SupplySetAsset
                {
                    AssetId = assetIdsByCode[member.AssetCode],
                    AmountPerUnit = member.AmountPerUnit,
                    SelectionWeight = member.SelectionWeight
                })
            ]
        };

        context.SupplySets.Add(set);
        await context.SaveChangesAsync(cancelToken);

        logger.LogInformation("Created supply set '{Code}' (id {Id}) with {MemberCount} members.", set.Code, set.Id, set.Assets.Count);

        return ServiceResult<SupplySetView>.Created(await ReadBackAsync(set.Id, cancelToken));
    }

    /// <summary>
    /// Reads the saved set back with its members' Assets attached, which the view needs for their codes and names.
    /// </summary>
    private async Task<SupplySetView> ReadBackAsync(int id, CancellationToken cancelToken)
    {
        SupplySet saved = await context.SupplySets
            .Include(set => set.Assets)
            .ThenInclude(member => member.Asset)
            .SingleAsync(set => set.Id == id, cancelToken);

        return SupplySetProjections.ToView(saved);
    }
}
