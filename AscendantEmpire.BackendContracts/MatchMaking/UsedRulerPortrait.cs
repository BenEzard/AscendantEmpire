namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// A ruler portrait already chosen by another player in the same game, and so unavailable.
/// </summary>
/// <remarks>
/// <para>
/// Shaped to match the image picker's own "used value" model, so the frontend can hand it straight to the control without a translation step - the
/// same arrangement as <see cref="UsedFactionColour"/>.
/// </para>
/// <para>
/// Only portraits somebody has actually chosen appear here. A slot still on the default carries no portrait to take, which is why any number of
/// players may be undecided at once.
/// </para>
/// </remarks>
public record UsedRulerPortrait
{
    /// <summary>The portrait that is taken, as its root-relative path.</summary>
    public required string Value { get; init; }

    /// <summary>The faction holding it, shown to explain why the portrait is unavailable.</summary>
    public required string UsedBy { get; init; }
}
