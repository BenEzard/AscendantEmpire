namespace AscendantEmpire.BackendContracts.CampaignMaps;

public class Coord : ICoord
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>
    /// Converts the coordinates to an EnhancedCoord object, which include the database ID of the record.
    /// </summary>
    public EnhancedCoord ToEnhancedCoord(int mapOffset, int mapWidth) => new()
    {
        X = X,
        Y = Y,
        DbId = mapOffset + (X + Y * mapWidth)
    };

    public int GetDbId(int mapOffset, int mapWidth)
    {
        return X + mapOffset + (Y * mapWidth);
    }

    public bool IsValid() => X >= 0 && Y >= 0;

    public override string ToString() => $"({X}, {Y})";

    public bool Equals(Coord? other) => other is not null && X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Coord other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);
}
