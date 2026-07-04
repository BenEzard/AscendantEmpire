namespace AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;

/// <summary>
/// A global/common unit template as returned by the dev design tool's create/update save endpoints.
/// </summary>
public record GlobalUnitTemplateView
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string MapImagePath { get; init; }

    public required int BodyComponentId { get; init; }

    public required string BodyComponentCode { get; init; }
}
