namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// A single authored start position in the export.
/// </summary>
public class MapExportStartPosition
{
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public string? DisplayName { get; init; }
    public int? LocationScore { get; init; }
}
