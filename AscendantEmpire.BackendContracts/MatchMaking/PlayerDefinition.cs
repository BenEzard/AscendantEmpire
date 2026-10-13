using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// The identity a user has given the player slot they control in one game, plus the colours their rivals have taken.
/// </summary>
/// <remarks>
/// <para>
/// Returned for the caller's own slot only. The slot is resolved from the game id and the token's user id, so this never describes anyone else.
/// </para>
/// <para>
/// <see cref="UsedFactionColours"/> and <see cref="UsedRulerPortraits"/> travel with the definition because the dialog that edits it needs a reading
/// taken at the moment it opens - the lobby listing the frontend already holds is cached, and deriving what is taken from that invites avoidable
/// clashes.
/// </para>
/// </remarks>
public record PlayerDefinition
{
    /// <summary>Database Id of the caller's player slot.</summary>
    public int PlayerId { get; init; }

    /// <summary>The game the slot belongs to.</summary>
    public int GameId { get; init; }

    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }

    /// <summary>Null when the player has not defined themselves yet.</summary>
    public RulerGender? RulerGender { get; init; }

    public required string FactionName { get; init; }

    /// <summary>Null when the player has not picked a colour yet.</summary>
    public string? FactionColour { get; init; }

    public required string PeopleName { get; init; }
    public required string Demonym { get; init; }
    public required string CultureName { get; init; }
    public required string Language { get; init; }

    /// <summary>Root-relative path to the ruler's portrait, e.g. "/portraits/ruler_07.png". Empty only on a slot predating the column.</summary>
    public required string RulerPortraitPath { get; init; }

    /// <summary>The colours the other players in this game already hold; never includes the caller's own.</summary>
    public required IReadOnlyCollection<UsedFactionColour> UsedFactionColours { get; init; }

    /// <summary>The portraits the other players in this game have already chosen; never includes the caller's own.</summary>
    public required IReadOnlyCollection<UsedRulerPortrait> UsedRulerPortraits { get; init; }

    /// <summary>
    /// True once play has begun, when every field but <see cref="RulerTitle"/> has become read-only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Travels with the definition so the setup dialog can render the locked fields disabled rather than let a player edit them and find out on
    /// submit. The server enforces the lock regardless - see <c>UpdatePlayerDefinitionHandler</c>, which from this point reads nothing out of a
    /// request but the title.
    /// </para>
    /// <para>
    /// The title is the one exception because it is expected to change as an empire grows, where the names around it are what everything else labels
    /// and attributes by for the life of the game.
    /// </para>
    /// </remarks>
    public bool IsLocked { get; init; }
}
