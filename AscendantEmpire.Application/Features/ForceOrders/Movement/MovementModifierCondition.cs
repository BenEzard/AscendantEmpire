namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

public sealed record MovementModifierCondition
{
    public required string Type { get; init; }
    public required string Operator { get; init; }
    public required string Value { get; init; }
}
