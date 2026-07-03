using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;

/// <summary>
/// A compiled (finalized) design's aggregated stats, resolved components and capabilities. Read without
/// the unlock filter, so it serves both the player's own finalized designs and enemy designs.
/// </summary>
public record CompiledUnitTemplateView
{
    public required int UnitTemplateId { get; init; }
    public required string DisplayName { get; init; }
    public required string MapImagePath { get; init; }
    public required int BodyComponentId { get; init; }
    public required DomainType MovementDomain { get; init; }

    public required long TotalWeight { get; init; }
    public required long TotalDetection { get; init; }
    public required long TotalVisibility { get; init; }

    // Movement/power/hitpoints are raw fixed-point longs (Scale 1,000; 1,000 = 1.0), copied from the body.
    public required long CampaignMovementPoints { get; init; }
    public required long BattlefieldMovementPoints { get; init; }
    public required long Power { get; init; }
    public required long Hitpoints { get; init; }
    public required int MinimumUnitSize { get; init; }
    public required int MaximumUnitSize { get; init; }

    public required CompiledDamageProfile Attack { get; init; }
    public required CompiledDamageProfile Resistance { get; init; }

    public required int CompiledOnTurn { get; init; }

    public required IReadOnlyCollection<CompiledComponentView> Components { get; init; }
    public required IReadOnlyCollection<CompiledCapabilityView> Capabilities { get; init; }
}
