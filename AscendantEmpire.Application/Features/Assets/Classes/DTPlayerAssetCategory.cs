using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTPlayerAssetCategory : IPlayerAssetCategory
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public int? SubjectPlayerId { get; set; }

    public required AssetCategoryType CategoryType { get; set; }

    public required int SortOrder { get; set; }

    public required IReadOnlyCollection<IPlayerAssetGroup> Groups { get; set; }

    public required IReadOnlyCollection<IPlayerAsset> Assets { get; set; }
}
