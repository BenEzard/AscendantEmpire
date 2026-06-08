namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// Root document serialised as <c>export.json</c> inside the export zip.
/// <para>
/// Versioning: increment <see cref="FormatVersion"/> when the schema changes in a
/// backwards-incompatible way so that importers can handle older files gracefully.
/// </para>
/// New map features (rivers, terrain bonuses, location-based resources) are added as
/// new top-level collections on this class, each referencing tile
/// <see cref="MapExportTile.RelativeId"/> values.
/// </summary>
public class MapExportDocument
{
    /// <summary>Schema version of this export format.</summary>
    public string FormatVersion { get; init; } = "1.0";

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
}
