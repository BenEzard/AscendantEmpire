namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// One of the eight compass directions a Force can step in on the campaign map.
/// </summary>
/// <remarks>
/// <para>
/// The campaign map is a square grid with eight-way adjacency, so the four diagonals are ordinary neighbours rather than a special case. These are
/// map directions, not screen directions: North is decreasing Y, which is upward on screen because tile rows are drawn top-down.
/// </para>
/// <para>
/// A single-tile step is expressed as a direction rather than a destination coordinate so the server, which already knows where the Force stands,
/// owns the neighbour and wrapping arithmetic. See <c>MapDirections</c> for the deltas.
/// </para>
/// </remarks>
public enum MoveDirection
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest,
}
