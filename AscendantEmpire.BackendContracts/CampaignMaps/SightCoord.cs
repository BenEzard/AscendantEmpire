namespace AscendantEmpire.BackendContracts.CampaignMaps;

public class SightCoord : EnhancedCoord
{
    public int Radius { get; set; }

    public List<SightCoord>? Children { get; set; }
}
