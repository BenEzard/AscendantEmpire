using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

public class DTCampaignMapTile : ICampaignMapTile
{
    public int CampaignMapGalleryIndexId { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public BiomeType BiomeTypeId { get; set; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// The river running along this tile's South edge — the edge it shares with the tile below it.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its North edge is the <c>SouthRiver</c> of the tile above it. The map editor reads and writes these
    /// two canonical columns directly, rather than the four derived edges the player map read sends.
    /// </remarks>
    public RiverWidth SouthRiver { get; set; }

    /// <summary>
    /// The river running along this tile's East edge — the edge it shares with the tile to its right.
    /// </summary>
    /// <remarks>
    /// A tile owns only its South and East edges, so its West edge is the <c>EastRiver</c> of the tile to its left.
    /// </remarks>
    public RiverWidth EastRiver { get; set; }
}
