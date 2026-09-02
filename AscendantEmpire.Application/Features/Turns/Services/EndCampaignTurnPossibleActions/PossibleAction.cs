using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;

/// <summary>
/// Represents a single action the player could take before ending their turn.
/// </summary>
public record PossibleAction : IPossibleAction
{
    public InformationCategory Category { get; init; }
    public required string Description { get; init; }
    public Coord? Coord { get; init; }
    public int Id { get; init; }
    public int RecordTypeId { get; init; }

    /// <summary>
    /// The action's tile, flattened out of <see cref="Coord"/> for the wire.
    /// </summary>
    /// <remarks>
    /// Checks work in <see cref="Coord"/> because that is what the map code passes around, but the client only needs the two numbers — and a nested
    /// object that is null for every off-map action is more awkward to read than a pair of nullable ints.
    /// </remarks>
    public int? XCoord => Coord?.X;

    /// <inheritdoc cref="XCoord"/>
    public int? YCoord => Coord?.Y;
}
