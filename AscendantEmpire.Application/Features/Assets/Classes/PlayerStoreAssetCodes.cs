namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// The catalogue codes of the <c>Player</c>-scope Assets that have somewhere to hold their figure.
/// </summary>
/// <remarks>
/// <para>
/// Matched on the catalogue code rather than the id, because the code is the stable key - the seed's upsert matches on it and never rewrites it, so renaming
/// the Asset in the catalogue leaves every site using these working.
/// </para>
/// <para>
/// One list rather than a copy per caller, because the spender and the read path have to agree on which codes have a store: a code the drawdown refuses and
/// the ticker reports a figure for would be two answers to one question. <see cref="AssetBankDrawdownService"/> routes a debit through these and
/// <c>PlayerAssetQuantityReader</c> resolves an amount through them.
/// </para>
/// <para>
/// <b>Culture is deliberately absent.</b> <c>Society.Experience.Culture</c> is the other <c>Player</c>-scope Asset in the catalogue, and nothing stores
/// culture points yet - so a request to spend them is refused loudly rather than quietly written somewhere that is not the number the panel reads, and its
/// ticker reads zero rather than a figure it invented. Adding that store is its own ticket, and when it lands it becomes a second entry here.
/// </para>
/// </remarks>
internal static class PlayerStoreAssetCodes
{
    /// <summary>
    /// The directed research points, held on <c>PlayerResearchState.UnspentDirectedPoints</c>.
    /// </summary>
    public const string TECHNOLOGY_EXPERIENCE = "Society.Experience.Technology";
}
