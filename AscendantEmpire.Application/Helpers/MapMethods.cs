using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Helpers;

internal static class MapMethods
{
    /// <summary>
    /// Converts the coordinates to EnhancedCoord objects, which include the map offset.
    /// </summary>
    internal static EnhancedCoord[] EnhanceCoordinates(int mapOffset, int mapWidth, IReadOnlyCollection<Coord> coordinates)
    {
        return [.. coordinates.Select(c => c.ToEnhancedCoord(mapOffset, mapWidth))];
    }

    internal static int GetDbId(int mapOffset, int mapWidth, int x, int y)
    {
        return new Coord { X = x, Y = y }.GetDbId(mapOffset, mapWidth);
    }

}
