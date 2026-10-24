using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One expense to draw out of a Player's holdings, naming either a specific Asset or an AssetGroup.
/// </summary>
/// <remarks>
/// <para>
/// The design doc gives an expense exactly two shapes - "10 Oak Logs" and "10 Logs, any kind" - and they behave differently, so they are built through
/// <see cref="OfAsset"/> and <see cref="OfGroup"/> rather than by setting one of two nullable columns. An expense naming both, or neither, is then not a
/// state the drawdown has to defend against.
/// </para>
/// <para>
/// The Settlement rather than the bank is what an expense carries, because that is what the caller has: a build order happens somewhere. Which bank that
/// Settlement draws on is the drawdown's business, and it changes as roads are built and pillaged.
/// </para>
/// </remarks>
public sealed record AssetExpense
{
    private AssetExpense()
    {
    }

    /// <summary>
    /// The Player paying, and the only one whose holdings may be touched.
    /// </summary>
    public int PlayerId { get; private init; }

    /// <summary>
    /// The Settlement the expense is incurred at, whose bank pays it.
    /// </summary>
    /// <remarks>Ignored for an Asset held per Player, which no Settlement's connectivity reaches - see <c>AssetStorageScope.Player</c>.</remarks>
    public int SettlementId { get; private init; }

    /// <summary>
    /// The one Asset to spend, or <see langword="null"/> where the expense names a group instead.
    /// </summary>
    public int? AssetId { get; private init; }

    /// <summary>
    /// The group to spend out of, in the Player's ordering, or <see langword="null"/> where the expense names one Asset instead.
    /// </summary>
    public int? AssetGroupId { get; private init; }

    /// <summary>
    /// How much to spend. Must be positive.
    /// </summary>
    public ScaledAmount Amount { get; private init; }

    /// <summary>
    /// "10 Oak Logs" - only that Asset is touched, whatever its group's ordering says.
    /// </summary>
    /// <remarks>
    /// A shortfall on the named Asset fails the expense outright. It deliberately does <b>not</b> fall through to the rest of the group: a caller naming an
    /// Asset has already decided which one it wants, and silently substituting Cedar for Oak would spend stock they were saving.
    /// </remarks>
    public static AssetExpense OfAsset(int playerId, int settlementId, int assetId, ScaledAmount amount)
    {
        return new AssetExpense
        {
            PlayerId = playerId,
            SettlementId = settlementId,
            AssetId = assetId,
            Amount = amount
        };
    }

    /// <summary>
    /// "10 Logs, any kind" - spent across the group in the Player's ordering, each Asset zeroed before the next is touched.
    /// </summary>
    /// <remarks>
    /// With an ordering of Oak, Generic, Cedar and a bank holding 4 Oak and 20 Generic, an expense of 10 takes Oak to zero and the remaining 6 out of
    /// Generic, leaving Cedar alone. If the group as a whole cannot cover the amount, nothing is spent at all.
    /// </remarks>
    public static AssetExpense OfGroup(int playerId, int settlementId, int assetGroupId, ScaledAmount amount)
    {
        return new AssetExpense
        {
            PlayerId = playerId,
            SettlementId = settlementId,
            AssetGroupId = assetGroupId,
            Amount = amount
        };
    }
}
