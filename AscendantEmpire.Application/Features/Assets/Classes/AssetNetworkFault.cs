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
/// Both kinds are faults only because connectivity is isolation today. Once roads, rivers and coastlines network settlements for real, a key spanning banks
/// that cannot reach each other joins this list - see CM-64, RS-48 and RS-49.
/// </remarks>
public enum AssetNetworkFaultKind
{
    /// <summary>The bank carries <see cref="System.Guid.Empty"/>, so it was saved before anything keyed it.</summary>
    Unkeyed = 1,

    /// <summary>The bank shares its key with another of the same Player's banks that it has no connection to.</summary>
    SharedKey = 2
}
