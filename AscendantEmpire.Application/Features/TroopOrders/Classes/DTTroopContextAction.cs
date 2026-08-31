using AscendantEmpire.Domain.Enums.Troops;
using AscendantEmpire.FrontendContracts.TroopOrders.GetTroopContextActions;

namespace AscendantEmpire.Application.Features.TroopOrders.Classes;

/// <inheritdoc cref="ITroopContextAction"/>
public class DTTroopContextAction : ITroopContextAction
{
    public TroopActionType ActionType { get; set; }

    public required string Label { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsEnabled { get; set; }

    public IReadOnlyCollection<string> DisabledReasons { get; set; } = [];
}
