using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTPlayerAssetMetaCategory : IPlayerAssetMetaCategory
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string IconPath { get; set; }

    public required int SortOrder { get; set; }

    public required IReadOnlyCollection<IPlayerAssetCategory> Categories { get; set; }
}
