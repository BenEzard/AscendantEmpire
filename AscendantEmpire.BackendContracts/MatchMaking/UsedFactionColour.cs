namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// A faction colour already held by another player in the same game, and so unavailable.
/// </summary>
/// <remarks>
/// <para>
/// Shaped to match the colour picker's own "used colour" model, so the frontend can hand it straight to the control without a translation step.
/// </para>
/// </remarks>
public record UsedFactionColour
{
    /// <summary>The colour that is taken, as a lower-cased "#rrggbb" string.</summary>
    public required string Value { get; init; }

    /// <summary>The faction holding it, shown to explain why the colour is unavailable.</summary>
    public required string UsedBy { get; init; }
}
