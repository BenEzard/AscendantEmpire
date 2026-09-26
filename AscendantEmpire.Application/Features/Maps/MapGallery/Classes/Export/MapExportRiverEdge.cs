using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// The river edges one tile owns, in the export.
/// </summary>
/// <remarks>
/// <para>
/// Only tiles carrying at least one river appear, so a map with no rivers has an empty <c>Rivers</c> section rather than a row per
/// tile. A tile absent from the section has no river on either of its edges.
/// </para>
/// <para>
/// Keyed by <see cref="RelativeId"/>, the same row-major <c>Y * Width + X</c> index <see cref="MapExportTile"/> uses, so an export
/// carries no database ids and the importer reconstructs the coordinates from the map's dimensions.
/// </para>
/// </remarks>
public class MapExportRiverEdge
{
    /// <summary>Row-major tile index: Y * Width + X (0-based).</summary>
    public int RelativeId { get; init; }

    /// <summary>The river along this tile's South edge — the edge it shares with the tile below it.</summary>
    public RiverWidth South { get; init; }

    /// <summary>The river along this tile's East edge — the edge it shares with the tile to its right.</summary>
    public RiverWidth East { get; init; }
}
