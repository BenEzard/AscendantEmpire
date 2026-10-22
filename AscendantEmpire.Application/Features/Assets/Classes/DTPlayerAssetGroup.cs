using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTPlayerAssetGroup : IPlayerAssetGroup
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public int? SubjectPlayerId { get; set; }

    public required int? SortOrder { get; set; }

    public required IReadOnlyCollection<IPlayerAsset> Assets { get; set; }
}
