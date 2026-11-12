using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;

/// <summary>
/// The payload for creating or updating one supply set from the Entity Setup Tool.
/// </summary>
/// <remarks>
/// Submitting a set reconciles its members to exactly <see cref="Assets"/> — a member left out is removed.
/// </remarks>
public record UpsertSupplySetDto
{
    /// <summary>
    /// Unique, stable key for the set, e.g. "human-food".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// The need the set satisfies, which decides how a requirement's quantity is measured.
    /// </summary>
    public required SupplyType SupplyType { get; init; }

    /// <summary>
    /// The Assets in the set. Must hold at least one: a set with no members can never be satisfied.
    /// </summary>
    public IReadOnlyCollection<UpsertSupplySetAssetDto> Assets { get; init; } = [];
}

/// <summary>
/// One Asset in a supply set, named by its code.
/// </summary>
/// <remarks>
/// A code rather than an id, so a set can be authored and seeded without knowing database identities.
/// </remarks>
public record UpsertSupplySetAssetDto
{
    public required string AssetCode { get; init; }

    /// <summary>
    /// How much of this Asset one unit of the need costs, scaled by <c>ScaledAmount.Scale</c> (1,000). Must be above zero.
    /// </summary>
    public required long AmountPerUnit { get; init; }

    /// <summary>
    /// How heavily this Asset is weighted when one member is drawn at random. Read only for a Repair set.
    /// </summary>
    public int SelectionWeight { get; init; } = 1;
}
