namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// Which of the two river edges a campaign tile owns is being named — its South edge or its East edge.
/// </summary>
/// <remarks>
/// <para>
/// A tile owns only these two, as its <c>SouthRiver</c> and <c>EastRiver</c> columns, so every edge on the map is stored exactly once. A tile's North
/// edge is the South edge of the tile above it, and its West edge is the East edge of the tile to its left. See
/// <c>docs/design/campaign-map/tiles.md</c>.
/// </para>
/// <para>
/// <b>Not persisted.</b> This exists so an authoring request can say <i>which</i> edge of a tile it means. Only South and East travel on the wire; a
/// client that wants to change a tile's North or West edge translates the click to the owning neighbour before it sends, which keeps the ownership
/// rule server-authoritative and stops two clients disagreeing about which row an edge lives on.
/// </para>
/// </remarks>
public enum RiverEdgeSide
{
    /// <summary>The edge this tile shares with the tile below it.</summary>
    South = 0,

    /// <summary>The edge this tile shares with the tile to its right.</summary>
    East = 1,
}
