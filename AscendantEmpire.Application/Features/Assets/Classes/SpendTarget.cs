namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What an <see cref="AssetExpense"/> is for: one named Asset, or any Asset out of one group.
/// </summary>
/// <remarks>
/// <para>
/// The two behave differently when spent, so they are built through <see cref="Asset"/> and <see cref="Group"/> rather than by setting one of two nullable
/// columns. A target naming both, or neither, is then not a state the drawdown has to defend against.
/// </para>
/// <para>
/// <b>It is its own type because an id cannot say which it is.</b> An Asset id and an AssetGroup id are both a bare <c>int</c>, so "10 Oak Logs" and "10
/// Logs, any kind" have the same signature; without this they would have to be told apart by the name of the factory that built the expense, which puts the
/// distinction in a method name rather than in the argument it describes.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for what each one spends.</para>
/// </remarks>
public sealed record SpendTarget
{
    private SpendTarget()
    {
    }

    /// <summary>
    /// The one Asset to spend, or <see langword="null"/> where the target names a group instead.
    /// </summary>
    public int? AssetId { get; private init; }

    /// <summary>
    /// The group to spend out of, in the Player's ordering, or <see langword="null"/> where the target names one Asset instead.
    /// </summary>
    public int? AssetGroupId { get; private init; }

    /// <summary>
    /// "Oak Logs" - only that Asset is touched, whatever its group's ordering says.
    /// </summary>
    /// <remarks>
    /// A shortfall on the named Asset fails the expense outright. It deliberately does <b>not</b> fall through to the rest of the group: a caller naming an
    /// Asset has already decided which one it wants, and silently substituting Cedar for Oak would spend stock they were saving.
    /// </remarks>
    public static SpendTarget Asset(int assetId)
    {
        return new SpendTarget { AssetId = assetId };
    }

    /// <summary>
    /// "Logs, any kind" - spent across the group in the Player's ordering, each Asset taken to nothing before the next is touched.
    /// </summary>
    /// <remarks>
    /// With an ordering of Oak, Generic, Cedar and 4 Oak and 20 Generic held, an expense of 10 takes Oak to zero and the remaining 6 out of Generic, leaving
    /// Cedar alone. If the group as a whole cannot cover the amount, nothing is spent at all.
    /// </remarks>
    public static SpendTarget Group(int assetGroupId)
    {
        return new SpendTarget { AssetGroupId = assetGroupId };
    }
}
