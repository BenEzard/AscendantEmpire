namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// A ruler portrait already chosen by another player in the same game, and so unavailable.
/// </summary>
public interface IUsedRulerPortrait
{
    /// <summary>The portrait that is taken, as its root-relative path.</summary>
    string Value { get; }

    /// <summary>The faction holding it, shown to explain why the portrait is unavailable.</summary>
    string UsedBy { get; }
}
