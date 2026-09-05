using AscendantEmpire.FrontendContracts.CampaignMaps;
using AscendantEmpire.FrontendContracts.ForceOrders.StepForce;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="IStepForceResult"/>
public class DTStepForceResult : IStepForceResult
{
    public int UnitInstanceId { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public decimal CampaignMovementRemaining { get; set; }
    public decimal CampaignMovementPoints { get; set; }
    public required IReadOnlyCollection<IObserver> Observers { get; set; }
}
