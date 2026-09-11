namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One tile of the path a <see cref="PlayerReport"/> saw its subject take.
/// </summary>
/// <remarks>
/// A child table rather than a JSON column, so a path can be drawn on the map and queried like any other tile reference, with a real foreign key to the tile.
/// </remarks>
public class PlayerReportPathTile
{
    public long PlayerReportId { get; set; }

    /// <summary>
    /// The tile's position along the path, from 0.
    /// </summary>
    public int Ordinal { get; set; }

    public int CampaignMapTileId { get; set; }

    public PlayerReport PlayerReport { get; set; } = null!;
}
