namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;

/// <summary>
/// A custom (player-owned) unit template as returned by the create/update save endpoints.
/// </summary>
public record UnitTemplateView
{
    public required int Id { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string MapImagePath { get; init; }

    public required int BodyComponentId { get; init; }

    public required string BodyComponentCode { get; init; }
}
