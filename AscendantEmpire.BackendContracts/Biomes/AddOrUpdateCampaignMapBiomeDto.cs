using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Biomes;

namespace AscendantEmpire.BackendContracts.Biomes;
public record class AddOrUpdateCampaignMapBiomeDto : IAddOrUpdateCampaignMapBiomeDto
{
    #region General Properties
    public required BiomeType BiomeType { get; init; }

    /// <inheritdoc />
    public required string Name { get; init; }

    /// <inheritdoc />
    public required decimal MovementCost { get; init; }

    /// <inheritdoc />
    public required bool DoesBlockSight { get; init; }

    /// <inheritdoc />
    public required bool IsWater { get; init; }

    /// <inheritdoc />
    public required bool IsTraversable { get; init; }

    /// <inheritdoc />
    public required decimal? RegenerationPercent { get; init; }
    #endregion

    #region Map Editor Properties
    /// <inheritdoc />
    public required bool IsPaintableOnCampaignMap { get; init; }

    /// <inheritdoc />
    public required string MapEditorHtmlColor { get; init; }

    /// <inheritdoc />
    public required bool CanBeElevated { get; init; }
    #endregion

    #region Passive Regenerating Resources
    /// <inheritdoc />
    public int? BaseForageValue { get; init; }

    /// <inheritdoc />
    public int? BaseHuntValue { get; init; }

    /// <inheritdoc />
    public int? BaseFishValue { get; init; }

    /// <inheritdoc />
    public int? BaseFodderValue { get; init; }
    #endregion

    #region Passive Material Values
    /// <inheritdoc />
    public int? BaseBoneValue { get; init; }

    /// <inheritdoc />
    public int? BaseWoodValue { get; init; }

    /// <inheritdoc />
    public int? BaseStoneValue { get; init; }
    #endregion
}
