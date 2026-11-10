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
    public required string PeopleName { get; set; }

    /// <inheritdoc />
    public required string Demonym { get; set; }

    /// <inheritdoc />
    public required string Language { get; set; }

    /// <inheritdoc />
    public required string RulerPortraitPath { get; set; }

    /// <inheritdoc />
    public required IReadOnlyCollection<DTUsedFactionColour> UsedFactionColours { get; set; }

    /// <inheritdoc />
    public required IReadOnlyCollection<DTUsedRulerPortrait> UsedRulerPortraits { get; set; }

    /// <inheritdoc />
    public bool IsLocked { get; set; }

    IReadOnlyCollection<IUsedFactionColour> IPlayerDefinition.UsedFactionColours => UsedFactionColours;

    IReadOnlyCollection<IUsedRulerPortrait> IPlayerDefinition.UsedRulerPortraits => UsedRulerPortraits;
}
