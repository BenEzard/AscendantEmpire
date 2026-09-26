using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

public interface ICampaignMapTile
{
    public int CampaignMapGalleryIndexId { get; }
    public int XCoord { get; }
    public int YCoord { get; }
    public BiomeType BiomeTypeId { get; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; }

    /// <summary>
    /// The river running along this tile's South edge — the edge it shares with the tile below it.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its North edge is the <c>SouthRiver</c> of the tile above it, and its West edge the <c>EastRiver</c>
    /// of the tile to its left. The map editor works in these canonical terms; the player map read derives all four of a tile's edges instead.
    /// </remarks>
    public RiverWidth SouthRiver { get; }

    /// <summary>
    /// The river running along this tile's East edge — the edge it shares with the tile to its right.
    /// </summary>
    public RiverWidth EastRiver { get; }
}
