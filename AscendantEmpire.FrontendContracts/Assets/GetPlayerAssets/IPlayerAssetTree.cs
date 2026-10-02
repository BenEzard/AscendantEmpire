namespace AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

/// <summary>
/// Everything the Assets toolbar and its panels need for the signed-in Player, in one tree.
/// </summary>
/// <remarks>
/// <para>
/// One endpoint rather than several: the toolbar needs the unlocked meta-category list before any panel is opened, and a category's or group's
/// visibility is a function of the Assets beneath it, so splitting the call would force the client to recombine what the server already knows.
/// </para>
/// <para>
/// Every member of every interface in this family must be declared on the interface, not only on the DT class implementing it. The nested collections
/// are typed by their interface, and System.Text.Json serialises collection elements by their declared type - a property missing from an interface is
/// silently dropped from the response.
/// </para>
/// </remarks>
public interface IPlayerAssetTree
{
    IReadOnlyCollection<IPlayerAssetMetaCategory> MetaCategories { get; }
}
