namespace AscendantEmpire.BackendContracts.CampaignMaps;

public class EnhancedCoord : Coord
{
    public int DbId { get; set; }

    public EnhancedCoord() { }

    public Coord ToCoord => new Coord { X = X, Y = Y };
}
