using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One Asset a caller wants the Player's current figure for, with the column that says where to look for it.
/// </summary>
/// <remarks>
/// <para>
/// The scope and the code travel with the id because the caller has already read them off the catalogue: a read path assembling a tree has the Asset rows in
/// hand, and making the quantity reader fetch them again would be a second trip for columns that are already loaded.
/// </para>
/// <para>
/// <see cref="Code"/> is what distinguishes one <c>Player</c>-scope Asset from another - see <see cref="PlayerStoreAssetCodes"/> - and is unused for the
/// other two scopes.
/// </para>
/// </remarks>
public sealed record AssetQuantityRequest(int AssetId, string Code, AssetStorageScope StorageScope);
