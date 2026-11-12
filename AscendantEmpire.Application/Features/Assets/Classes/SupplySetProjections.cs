using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// Shared mapping and Asset resolution for the supply set handlers.
/// </summary>
/// <remarks>
/// A set's members arrive as Asset codes and are stored as ids, and every write path returns the same view, so both belong in one place rather than in
/// each handler.
/// </remarks>
internal static class SupplySetProjections
{
    /// <summary>
    /// Projects a persisted set onto the API view. The set's members must already have their <c>Asset</c> loaded.
    /// </summary>
    public static SupplySetView ToView(SupplySet set)
    {
        return new SupplySetView
        {
            Id = set.Id,
            Code = set.Code,
            DisplayName = set.DisplayName,
            SupplyType = set.SupplyType,
            Assets =
            [
                .. set.Assets
                    .OrderBy(member => member.Asset.Code)
                    .Select(member => new SupplySetAssetView
                    {
                        AssetId = member.AssetId,
                        AssetCode = member.Asset.Code,
                        AssetDisplayName = member.Asset.DisplayName,
                        AmountPerUnit = member.AmountPerUnit,
                        SelectionWeight = member.SelectionWeight
                    })
            ]
        };
    }

    /// <summary>
    /// Resolves the payload's Asset codes to ids, keyed case-insensitively.
    /// </summary>
    /// <remarks>
    /// The codes that did not resolve are returned rather than thrown, so one response can name every one of them instead of failing a code at a time.
    /// </remarks>
    public static async Task<(Dictionary<string, int> AssetIdsByCode, string[] UnresolvedCodes)> ResolveAssetsAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<UpsertSupplySetAssetDto> members,
        CancellationToken cancelToken)
    {
        string[] codes = [.. members.Select(member => member.AssetCode).Distinct(StringComparer.OrdinalIgnoreCase)];

        List<(string Code, int Id)> found = await context.Assets
            .Where(asset => codes.Contains(asset.Code))
            .Select(asset => new ValueTuple<string, int>(asset.Code, asset.Id))
            .ToListAsync(cancelToken);

        Dictionary<string, int> assetIdsByCode = found.ToDictionary(asset => asset.Code, asset => asset.Id, StringComparer.OrdinalIgnoreCase);

        string[] unresolvedCodes = [.. codes.Where(code => !assetIdsByCode.ContainsKey(code)).Order()];

        return (assetIdsByCode, unresolvedCodes);
    }

    /// <summary>
    /// One error message per Asset code that did not resolve.
    /// </summary>
    public static List<string> DescribeUnresolved(string[] unresolvedCodes)
    {
        return [.. unresolvedCodes.Select(code => $"No Asset exists with code '{code}'.")];
    }
}
