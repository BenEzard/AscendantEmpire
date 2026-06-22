using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;

/// <summary>
/// A component fitted into one of a template's slots. Components form a tree: a row with a null
/// <see cref="ParentTemplateComponentId"/> is fitted into a body slot, otherwise into a slot of the
/// parent fitted-component identified by that id.
/// </summary>
public record DesignTemplateComponent
{
    /// <summary>The fitted-component row id, so the client can reconstruct the nesting tree.</summary>
    public required int Id { get; init; }

    /// <summary>The parent fitted-component this one nests inside; null when fitted into a body slot.</summary>
    public int? ParentTemplateComponentId { get; init; }

    public required int UnitComponentSlotId { get; init; }
    public required int UnitComponentId { get; init; }
    public required string ComponentCode { get; init; }
    public required string ComponentDisplayName { get; init; }
    public required ComponentType ComponentType { get; init; }
}
