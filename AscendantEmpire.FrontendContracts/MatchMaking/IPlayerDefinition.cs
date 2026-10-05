using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// The identity a user has given their own player slot in one game, plus the colours their rivals have taken.
/// </summary>
public interface IPlayerDefinition
{
    /// <summary>Database Id of the caller's player slot.</summary>
    int PlayerId { get; }

    /// <summary>The game the slot belongs to.</summary>
    int GameId { get; }

    /// <summary>The ruler's title, e.g. "Queen". Never blank - an unset title reads as the default placeholder.</summary>
    string RulerTitle { get; }

    /// <summary>The ruler's name.</summary>
    string RulerName { get; }

    /// <summary>The ruler's gender, or null when the player has not defined themselves yet.</summary>
    RulerGender? RulerGender { get; }

    /// <summary>The faction name.</summary>
    string FactionName { get; }

    /// <summary>The faction's colour as an "#rrggbb" string, or null when none has been picked.</summary>
    string? FactionColour { get; }

    /// <summary>What this faction's people call themselves as a group, e.g. "The Vashti".</summary>
    string PeopleName { get; }

    /// <summary>What one member of that people is called, e.g. "Vashti".</summary>
    string Demonym { get; }

    /// <summary>The name of this people's culture, e.g. "Vashti Highland".</summary>
    string CultureName { get; }

    /// <summary>The language this people speak, e.g. "Old Vashti".</summary>
    string Language { get; }

    /// <summary>The colours the other players in this game already hold; never includes the caller's own.</summary>
    IReadOnlyCollection<IUsedFactionColour> UsedFactionColours { get; }
}
