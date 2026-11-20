using AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="ITakeResourcesOptions"/>
public class DTTakeResourcesOptions : ITakeResourcesOptions
{
    public IReadOnlyCollection<ITakeResourcesOption> Resources { get; set; } = [];
}

/// <inheritdoc cref="ITakeResourcesOption"/>
public class DTTakeResourcesOption : ITakeResourcesOption
{
    public int MapResourceInstanceId { get; set; }

    public byte Slot { get; set; }

    public required string DisplayName { get; set; }

    public required string? IconPath { get; set; }

    public int UnitsWouldTake { get; set; }

    public IReadOnlyCollection<ITakeResourcesAssetLine> WouldGain { get; set; } = [];
}
