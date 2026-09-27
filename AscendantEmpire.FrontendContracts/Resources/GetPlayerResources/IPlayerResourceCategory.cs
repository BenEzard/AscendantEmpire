using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.FrontendContracts.Resources.GetPlayerResources;

/// <summary>
/// One category visible to the Player, holding either groups of Resources or Resources directly.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Groups"/> and <see cref="Resources"/> is populated, chosen by <see cref="CategoryType"/>; the other is empty rather than
/// null.
/// </remarks>
public interface IPlayerResourceCategory
{
    int Id { get; }

    /// <summary>
    /// The category's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// Whether this category holds groups or Resources directly, which is what picks the layout the client draws.
    /// </summary>
    ResourceCategoryType CategoryType { get; }

    int SortOrder { get; }

    IReadOnlyCollection<IPlayerResourceGroup> Groups { get; }

    IReadOnlyCollection<IPlayerResource> Resources { get; }
}
