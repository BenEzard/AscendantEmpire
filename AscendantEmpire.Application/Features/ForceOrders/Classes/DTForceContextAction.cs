using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.FrontendContracts.ForceOrders.GetForceContextActions;

namespace AscendantEmpire.Application.Features.ForceOrders.Classes;

/// <inheritdoc cref="IForceContextAction"/>
public class DTForceContextAction : IForceContextAction
{
    public ForceActionType ActionType { get; set; }

    public required string Label { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsEnabled { get; set; }

    public IReadOnlyCollection<string> DisabledReasons { get; set; } = [];
}
