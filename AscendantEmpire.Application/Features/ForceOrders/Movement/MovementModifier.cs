using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// Represents one movement-related modifier parsed from <c>CompiledDesignStats.ModifiersJson</c>.
/// The payload is intentionally richer than a simple enum because many movement rules are
/// conditional and data-driven.
/// </summary>
public sealed record MovementModifier
{
    public required string Category { get; init; }
    public required string Effect { get; init; }
    public decimal? Value { get; init; }
    public DomainType? DomainType { get; init; }
    public IReadOnlyList<MovementModifierCondition> Conditions { get; init; } = [];
}
