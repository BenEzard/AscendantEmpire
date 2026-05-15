using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;


/// <summary>
/// Represents a single action the player could take before ending their turn.
/// </summary>
public record PossibleAction
{
    public InformationCategory Category { get; init; }
    public required string Description { get; init; }
    public Coord? Coord { get; init; }
    public int Id { get; init; }
    public int RecordTypeId { get; init; }
}
