namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// How wide a river running along one edge of a campaign tile is, or <see cref="None"/> for an edge with no river.
/// </summary>
/// <remarks>
/// <para>
/// Rivers run <b>between</b> tiles, never on them. Each tile owns only its South and East edges (<c>SouthRiver</c> / <c>EastRiver</c>), so every edge is
/// stored exactly once and the two banks of a river can never disagree about it. See <c>docs/design/campaign-map/tiles.md</c>.
/// </para>
/// <para>
/// <b>Persisted</b>, and deliberately a <see cref="byte"/> so it stores as <c>tinyint</c>: both columns sit on every tile of every map and every game,
/// so a 40,000 tile map carries 80,000 of them. Two bytes a row instead of eight is worth having for a value with three members. Keeping the widths on
/// the tile rather than in a sparse edge table is discussed in <c>docs/design/campaign-map/tiles.md</c>.
/// </para>
/// <para>
/// The values are <b>ordered by width</b>: the movement rules take the larger or smaller of two widths by comparing them. A member may be renamed but
/// never reordered or renumbered, and a new width belongs in its place in that order.
/// </para>
/// </remarks>
public enum RiverWidth : byte
{
    /// <summary>No river along this edge.</summary>
    None = 0,

    /// <summary>A river a Land Force can cross, at a cost.</summary>
    Medium = 1,

    /// <summary>A river no Land Force can cross until bridges or fords exist, and which Water Forces can sail up.</summary>
    Large = 2,
}
