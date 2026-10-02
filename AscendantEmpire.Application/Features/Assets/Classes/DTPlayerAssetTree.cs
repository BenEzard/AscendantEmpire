using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// The wire shape of the signed-in Player's visible asset tree.
/// </summary>
/// <remarks>
/// Nothing is computed across this family - the controller transcribes the handler's result records field for field. Every property must also appear
/// on the interface it implements, or System.Text.Json drops it when serialising the nested collections by their declared type.
/// </remarks>
public class DTPlayerAssetTree : IPlayerAssetTree
{
    public required IReadOnlyCollection<IPlayerAssetMetaCategory> MetaCategories { get; set; }
}
