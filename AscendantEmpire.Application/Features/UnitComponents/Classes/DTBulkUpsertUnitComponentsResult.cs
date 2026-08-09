using AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

namespace AscendantEmpire.Application.Features.UnitComponents.Classes;

/// <summary>
/// Frontend-facing implementation of <see cref="IBulkUpsertUnitComponentsResult"/>.
/// </summary>
public class DTBulkUpsertUnitComponentsResult : IBulkUpsertUnitComponentsResult
{
    public required int AddedCount { get; set; }
    public required int UpdatedCount { get; set; }
    public required IReadOnlyCollection<string> UpdatedCodes { get; set; }
    public required int RemovedSlotCount { get; set; }
    public required IReadOnlyCollection<string> UnfittedTemplateNames { get; set; }
}
