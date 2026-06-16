namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// A single player slot within a game, together with whether it has already been claimed.
/// </summary>
/// <remarks>
/// <para>
/// A slot is just a <c>game.Player</c> row. An unclaimed slot has a <c>null</c> <c>UserId</c>;
/// claiming it sets that <c>UserId</c> to the requesting account.
/// </para>
/// </remarks>
public record MatchmakingSlot
{
    public int PlayerId { get; init; }
    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }
    public required string FactionName { get; init; }

    /// <summary>A friendly one-line label, e.g. "King Player 1 (Faction 1)".</summary>
    public required string RulerLabel { get; init; }

    /// <summary>True when some account already controls this slot.</summary>
    public bool IsClaimed { get; init; }

    /// <summary>True when the account making the request is the one that controls this slot.</summary>
    public bool IsClaimedByCurrentUser { get; init; }
}
