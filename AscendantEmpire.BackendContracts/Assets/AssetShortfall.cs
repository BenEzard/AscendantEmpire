namespace AscendantEmpire.BackendContracts.Assets;

/// <summary>
/// What an expense was short of, and by how much, carried alongside the refusal's message.
/// </summary>
/// <remarks>
/// <para>
/// The caller of a drawdown is a build order or a technology purchase that has to tell the Player why it was refused, and some of them have to do more than
/// print the sentence - offer the missing amount on the market, or queue the order behind production. That needs the numbers rather than the prose, which is
/// what this is for; see <c>ServiceResultBase.ErrorDetail</c>.
/// </para>
/// <para>Every amount is scaled by <c>ScaledAmount.Scale</c> (1,000), on the same terms as <see cref="GetAssetBanks.AssetBankHolding.RawQuantity"/>.</para>
/// </remarks>
public record AssetShortfall
{
    /// <summary>
    /// The Asset the expense named, or <see langword="null"/> where it named a group.
    /// </summary>
    public int? AssetId { get; init; }

    /// <summary>
    /// The AssetGroup the expense named, or <see langword="null"/> where it named a single Asset.
    /// </summary>
    public int? AssetGroupId { get; init; }

    /// <summary>
    /// What the Player is short of, named as they would see it - "Logs" for a group expense, "Oak Log" for an Asset one.
    /// </summary>
    public required string DisplayName { get; init; }

    public required long RawRequired { get; init; }

    /// <summary>
    /// What was actually there: the single Asset's balance, or the sum of every balance the group ordering would have walked.
    /// </summary>
    public required long RawAvailable { get; init; }

    /// <summary>
    /// <see cref="RawRequired"/> less <see cref="RawAvailable"/>. Always positive - a satisfiable expense carries no shortfall.
    /// </summary>
    public required long RawShortfall { get; init; }
}
