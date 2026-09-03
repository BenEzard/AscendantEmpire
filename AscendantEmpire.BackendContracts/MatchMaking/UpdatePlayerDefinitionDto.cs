using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// Request body for defining the player slot the caller controls in a game. A full replacement of the five values, not a patch.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately carries no game or player id: the game comes from the route and the slot within it is resolved from the token's user id, so the body
/// cannot name a slot the caller does not own.
/// </para>
/// </remarks>
public record class UpdatePlayerDefinitionDto
{
    /// <summary>The ruler's title. Blank is allowed and stored as the default placeholder title.</summary>
    public string? RulerTitle { get; init; }

    public required string RulerName { get; init; }
    public required RulerGender RulerGender { get; init; }
    public required string FactionName { get; init; }

    /// <summary>The faction's colour as an "#rrggbb" string. Stored lower-cased and unique within the game.</summary>
    public required string FactionColour { get; init; }
}
