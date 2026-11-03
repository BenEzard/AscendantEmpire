namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One bank whose <c>NetworkKey</c> disagrees with what a recompute would give it.
/// </summary>
/// <remarks>
/// An application diagnostic rather than a game concept, which is why the kind lives here beside it and not in <c>Domain.Enums</c>. The key is derived data
/// with no referential integrity, so a check that can name the wrong rows is the only thing standing between a stale key and the wrong Settlements reaching
/// each other.
/// </remarks>
public sealed record AssetNetworkFault(int AssetBankId, int SettlementId, AssetNetworkFaultKind Kind);

/// <summary>
/// What is wrong with a bank's <c>NetworkKey</c>.
/// </summary>
/// <remarks>
/// Values are assigned explicitly: they are an application diagnostic rather than a persisted column, but a log line quoting one has to keep meaning the same thing.
/// What networks two banks today is a camp feeding its parent, so <see cref="Unjoined"/> is the fault of a camp that is not in its parent's network. Once roads, rivers
/// and coastlines network settlements for real, that kind covers their connections too - see CM-64, RS-48 and RS-49.
/// </remarks>
public enum AssetNetworkFaultKind
{
    /// <summary>The bank carries <see cref="System.Guid.Empty"/>, so it was saved before anything keyed it.</summary>
    Unkeyed = 1,

    /// <summary>The bank shares its key with another of the same Player's banks that it has no connection to.</summary>
    /// <remarks>The dangerous one: this is how a Settlement comes to spend out of a store it cannot reach.</remarks>
    SharedKey = 2,

    /// <summary>The bank is in a network of its own when it should be sharing one, so it reaches less than it should.</summary>
    /// <remarks>Costs the Player reach rather than letting them spend what is not theirs, which is why it is the fault a re-key reports last.</remarks>
    Unjoined = 3
}
