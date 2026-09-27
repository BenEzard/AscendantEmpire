using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.BackendContracts.Resources.GetPlayerResources;

/// <summary>
/// One category visible to a Player, holding either groups of Resources or Resources directly.
/// </summary>
/// <remarks>
/// <para>
/// Categories carry no unlock rows of their own. One is here because at least one Resource beneath it is available to the Player, and it disappears
/// with the last of them, so its visibility can never disagree with its contents.
/// </para>
/// <para>
/// Exactly one of <see cref="Groups"/> and <see cref="Resources"/> is populated, chosen by <see cref="CategoryType"/>. The other is empty rather than
/// null, so a client can render whichever arm it recognises without a null check.
/// </para>
/// </remarks>
public record PlayerResourceCategory
{
    public required int Id { get; init; }

    /// <summary>
    /// The category's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether this category holds groups or Resources directly, which is what picks the layout the client draws.
    /// </summary>
    public required ResourceCategoryType CategoryType { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>
    /// The visible groups beneath this category, in display order. Empty unless <see cref="CategoryType"/> is GroupedResources.
    /// </summary>
    public required IReadOnlyCollection<PlayerResourceGroup> Groups { get; init; }

    /// <summary>
    /// The visible Resources hanging directly off this category, in display order. Empty unless <see cref="CategoryType"/> is StandaloneResources.
    /// </summary>
    public required IReadOnlyCollection<PlayerResource> Resources { get; init; }
}
