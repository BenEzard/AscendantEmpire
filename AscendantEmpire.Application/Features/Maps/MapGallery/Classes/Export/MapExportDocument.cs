namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// Root document serialised as <c>export.json</c> inside the export zip.
/// <para>
/// Versioning: increment <see cref="FormatVersion"/> when the schema changes in a
/// backwards-incompatible way so that importers can handle older files gracefully.
/// </para>
/// New map features (terrain bonuses, location-based resources) are added as
/// new top-level collections on this class, each referencing tile
/// <see cref="MapExportTile.RelativeId"/> values, as <see cref="Rivers"/> does.
/// </summary>
public class MapExportDocument
{
    /// <summary>
    /// Schema version of this export format.
    /// </summary>
    /// <remarks>
    /// 1.1 added <see cref="Rivers"/>. A 1.0 document still imports: a missing section means a map with no rivers on it, which is
    /// exactly what every map written before they existed was.
    /// </remarks>
    public string FormatVersion { get; init; } = "1.1";

    /// <summary>UTC timestamp at which the export was created.</summary>
    public DateTime ExportedAt { get; init; }

    /// <summary>Map metadata (portable, no DB IDs).</summary>
    public required MapExportIndex Map { get; init; }

    /// <summary>
    /// All tiles, ordered by <see cref="MapExportTile.RelativeId"/> ascending.
    /// </summary>
    public IReadOnlyList<MapExportTile> Tiles { get; init; } = [];

    /// <summary>Biomes used by at least one tile in <see cref="Tiles"/>.</summary>
    public IReadOnlyList<MapExportBiome> Biomes { get; init; } = [];

    /// <summary>Geographies used by at least one tile in <see cref="Tiles"/>.</summary>
    public IReadOnlyList<MapExportGeography> Geographies { get; init; } = [];

    /// <summary>Authored player start positions for the exported map.</summary>
    public IReadOnlyList<MapExportStartPosition> StartPositions { get; init; } = [];

    /// <summary>
    /// The river edges on the map, for the tiles that own at least one, ordered by <see cref="MapExportRiverEdge.RelativeId"/>.
    /// </summary>
    /// <remarks>
    /// Sparse rather than one row per tile, because rivers border a small minority of a map's tiles and an export is read by people
    /// as well as by the importer. Empty, or absent altogether in a 1.0 document, means a map with no rivers.
    /// </remarks>
    public IReadOnlyList<MapExportRiverEdge> Rivers { get; init; } = [];
}
