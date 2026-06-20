namespace AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;

/// <summary>
/// A unit template available to the player: either a global/common template they have unlocked or one
/// of their own custom (player-owned) templates, with its body and the components fitted into its slots.
/// </summary>
public record DesignTemplate
{
    public required int Id { get; init; }

    /// <summary>Stable key for global/common templates; null for custom player-owned ones.</summary>
    public string? Code { get; init; }

    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string MapImagePath { get; init; }

    /// <summary>True when this is the player's own custom template; false for a global/common one.</summary>
    public required bool IsCustom { get; init; }

    /// <summary>Whether the player has prototyped (finalized) this template; false when not yet.</summary>
    public required bool HasBeenPrototyped { get; init; }

    public required int BodyComponentId { get; init; }
    public required string BodyComponentCode { get; init; }
    public required string BodyComponentDisplayName { get; init; }

    public required IReadOnlyCollection<DesignTemplateComponent> Components { get; init; }
}
