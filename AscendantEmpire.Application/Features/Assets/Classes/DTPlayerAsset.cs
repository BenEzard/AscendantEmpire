using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.FrontendContracts.Assets.GetPlayerAssets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

public class DTPlayerAsset : IPlayerAsset
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string? Description { get; set; }

    public required string IconPath { get; set; }

    public required bool IsGradedAsset { get; set; }

    public required AssetGrade AssetGrade { get; set; }

    public required int SortOrder { get; set; }

    public required bool NeverLuxury { get; set; }

    public required int? ObsoleteInRounds { get; set; }

    public required int Amount { get; set; }

    public required int AmountAddedPerTurn { get; set; }

    public required bool HasStrategicReserve { get; set; }

    public required bool IsImported { get; set; }

    public required bool IsExported { get; set; }

    public required bool DemandNotMet { get; set; }

    public required bool IsLuxury { get; set; }
}
