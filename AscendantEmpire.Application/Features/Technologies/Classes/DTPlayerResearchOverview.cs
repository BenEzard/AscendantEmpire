using AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTPlayerResearchOverview : IPlayerResearchOverview
{
    public int? CurrentTechnologyId { get; set; }

    public string? CurrentTechnologyDisplayName { get; set; }

    public int? NextTechnologyId { get; set; }

    public string? NextTechnologyDisplayName { get; set; }

    public required int UnspentDirectedPoints { get; set; }

    public required int UnspentDiffusionPoints { get; set; }

    public required int CurrentRoundNumber { get; set; }

    public required IReadOnlyCollection<IResearchableTechnology> Researchable { get; set; }
}
