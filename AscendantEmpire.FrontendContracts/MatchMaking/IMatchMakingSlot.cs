namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// A single player slot within a game, plus whether it has already been claimed.
/// </summary>
public interface IMatchMakingSlot
{
    /// <summary>Database Id of the player slot.</summary>
    int PlayerId { get; }

    /// <summary>The ruler's title, e.g. "King".</summary>
    string RulerTitle { get; }

    /// <summary>The ruler's name, e.g. "Player 1".</summary>
    string RulerName { get; }

    /// <summary>The faction name, e.g. "Faction 1".</summary>
    string FactionName { get; }

    /// <summary>A friendly one-line label combining the title, name and faction.</summary>
    string RulerLabel { get; }

    /// <summary>True when some account already controls this slot (it cannot be claimed).</summary>
    bool IsClaimed { get; }

    /// <summary>True when the requesting account is the one that controls this slot.</summary>
    bool IsClaimedByCurrentUser { get; }
}
