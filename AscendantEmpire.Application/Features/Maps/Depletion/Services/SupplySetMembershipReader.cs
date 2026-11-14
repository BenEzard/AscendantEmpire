using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="ISupplySetMembershipReader" />
internal sealed class SupplySetMembershipReader : ISupplySetMembershipReader
{
    public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>>> GetAmountsPerUnitAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> supplySetIds,
        CancellationToken cancelToken = default)
    {
        int[] distinctIds = [.. supplySetIds.Distinct()];

        var members = await context.SupplySetAssets
            .Where(member => distinctIds.Contains(member.SupplySetId))
            .Select(member => new { member.SupplySetId, member.AssetId, member.AmountPerUnit })
            .ToListAsync(cancelToken);

        var membersBySetId = members
            .GroupBy(member => member.SupplySetId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, long>)group.ToDictionary(member => member.AssetId, member => member.AmountPerUnit));

        // A set with no members at all would otherwise be missing, and a caller reading it would see a set it could not find rather than a set that feeds nobody.
        foreach (int supplySetId in distinctIds.Where(id => membersBySetId.ContainsKey(id) == false))
        {
            membersBySetId.Add(supplySetId, new Dictionary<int, long>());
        }

        return membersBySetId;
    }
}
