using AscendantEmpire.Domain.Enums.Resources;
using AscendantEmpire.FrontendContracts.Resources.GetPlayerResources;

namespace AscendantEmpire.Application.Features.Resources.Classes;

public class DTPlayerResource : IPlayerResource
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string? Description { get; set; }

    public required string IconPath { get; set; }

    public required bool IsGradedResource { get; set; }

    public required ResourceGrade ResourceGrade { get; set; }

    public required int SortOrder { get; set; }

    public required bool NeverLuxury { get; set; }

    public required int? ObsoleteInRounds { get; set; }

    public required int Amount { get; set; }

    public required int AmountAddedPerTurn { get; set; }

    public required bool HasStrategicStockpile { get; set; }

    public required bool IsImported { get; set; }

    public required bool IsExported { get; set; }

    public required bool DemandNotMet { get; set; }

    public required bool IsLuxury { get; set; }
}
