using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.FrontendContracts.MatchMaking;

namespace AscendantEmpire.Application.Features.MatchMaking.Classes;

public class DTPlayerDefinition : IPlayerDefinition
{
    /// <inheritdoc />
    public int PlayerId { get; set; }

    /// <inheritdoc />
    public int GameId { get; set; }

    /// <inheritdoc />
    public required string RulerTitle { get; set; }

    /// <inheritdoc />
    public required string RulerName { get; set; }

    /// <inheritdoc />
    public RulerGender? RulerGender { get; set; }

    /// <inheritdoc />
    public required string FactionName { get; set; }

    /// <inheritdoc />
    public string? FactionColour { get; set; }

    /// <inheritdoc />
    public required IReadOnlyCollection<DTUsedFactionColour> UsedFactionColours { get; set; }

    IReadOnlyCollection<IUsedFactionColour> IPlayerDefinition.UsedFactionColours => UsedFactionColours;
}
