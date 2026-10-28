using AscendantEmpire.Domain.Enums.UnitDesigns;
using AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ISplitForceOptions"/>
public class DTSplitForceOptions : ISplitForceOptions
{
    public int UnitInstanceId { get; set; }

    public required string DisplayName { get; set; }

    /// <remarks>
    /// Required so that a projection which forgets it is a build error. Commissioned is the enum's first member, so an unset value is not merely blank —
    /// it says the Force is commissioned, which the dialog would then put in its title.
    /// </remarks>
    public required LeadershipModel LeadershipModel { get; set; }

    public int HealthyCount { get; set; }

    public int InjuredCount { get; set; }

    public int TotalStrength { get; set; }

    public int BaseSize { get; set; }
}
