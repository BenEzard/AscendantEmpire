using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets.GetSupplySets;

/// <summary>
/// Every supply set in the catalogue, ordered by code.
/// </summary>
public record GetSupplySetsResult
{
    public IReadOnlyCollection<SupplySetView> SupplySets { get; init; } = [];
}

/// <summary>
/// One supply set and its members.
/// </summary>
public record SupplySetView
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public required SupplyType SupplyType { get; init; }

    public IReadOnlyCollection<SupplySetAssetView> Assets { get; init; } = [];
}

/// <summary>
/// One Asset in a supply set, with what it costs and how heavily it is weighted.
/// </summary>
public record SupplySetAssetView
{
    public required int AssetId { get; init; }

    public required string AssetCode { get; init; }

    public required string AssetDisplayName { get; init; }

    /// <summary>
    /// How much of this Asset one unit of the need costs, scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public required long AmountPerUnit { get; init; }

    public required int SelectionWeight { get; init; }
}
