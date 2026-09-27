using AscendantEmpire.FrontendContracts.Resources.GetPlayerResources;

namespace AscendantEmpire.Application.Features.Resources.Classes;

/// <summary>
/// The wire shape of the signed-in Player's visible resource tree.
/// </summary>
/// <remarks>
/// Nothing is computed across this family - the controller transcribes the handler's result records field for field. Every property must also appear
/// on the interface it implements, or System.Text.Json drops it when serialising the nested collections by their declared type.
/// </remarks>
public class DTPlayerResourceTree : IPlayerResourceTree
{
    public required IReadOnlyCollection<IPlayerResourceMetaCategory> MetaCategories { get; set; }
}
