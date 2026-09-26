using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

/// <summary>
/// One river edge to set, named by the tile that owns it and which of that tile's two edges it is.
/// </summary>
/// <remarks>
/// Only South and East can be named, because they are the only edges a tile owns. A client changing a tile's North or West edge translates the click to
/// the neighbour that owns it first, wrapping included, so that every edge on the map has exactly one way to be addressed.
/// </remarks>
public interface IRiverEdgeChange
{
    /// <summary>The X coordinate of the tile that owns the edge.</summary>
    int X { get; }

    /// <summary>The Y coordinate of the tile that owns the edge.</summary>
    int Y { get; }

    /// <summary>Which of the owning tile's two edges this is.</summary>
    RiverEdgeSide Edge { get; }

    /// <summary>The width to set the edge to. <see cref="RiverWidth.None"/> erases the river from it.</summary>
    RiverWidth Width { get; }
}
