using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

public class DTObservedCampaignTile : IObservedCampaignTile
{
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public BiomeType BiomeTypeId { get; set; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    public bool IsExplored { get; set; }

    /// <summary>
    /// The Tile's four river edges, or <see langword="null"/> for ground the Player has never explored.
    /// </summary>
    /// <remarks>
    /// The contract record itself rather than a copy of it: it is an immutable value with no navigation of its own, so there is nothing for a
    /// presentation type to add.
    /// </remarks>
    public TileRivers? Rivers { get; set; }

    public IReadOnlyCollection<DTStructureStub>? Structures { get; set; }

    public IReadOnlyCollection<DTForceStub>? Forces { get; set; }

    ITileRivers? IObservedCampaignTile.Rivers => Rivers;

    IReadOnlyCollection<IStructureStub>? IObservedCampaignTile.Structures => Structures;

    IReadOnlyCollection<IForceStub>? IObservedCampaignTile.Forces => Forces;
}
