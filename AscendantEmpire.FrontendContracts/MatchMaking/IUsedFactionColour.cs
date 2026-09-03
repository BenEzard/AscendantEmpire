namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// A faction colour already held by another player in the same game, and so unavailable.
/// </summary>
public interface IUsedFactionColour
{
    /// <summary>The colour that is taken, as a lower-cased "#rrggbb" string.</summary>
    string Value { get; }

    /// <summary>The faction holding it, shown to explain why the colour is unavailable.</summary>
    string UsedBy { get; }
}
