using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// A single tile in the export.
/// <para>
/// <see cref="RelativeId"/> is the row-major tile index: <c>Y * Width + X</c>.
/// The importer reconstructs X/Y from this value and the map dimensions.
/// Future additions (rivers, terrain bonuses, etc.) reference <see cref="RelativeId"/>
/// to link back to specific tiles.
/// </para>
/// </summary>
public class MapExportTile
{
    /// <summary>Row-major tile index: Y * Width + X (0-based).</summary>
    public int RelativeId { get; init; }

    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType GeographyTypeId { get; init; }
}
