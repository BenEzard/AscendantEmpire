using AscendantEmpire.Domain.Entities.Settlements;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Joins one Settlement to the <see cref="AssetBank"/> it currently draws on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The primary key is <see cref="SettlementId"/>, not a pair.</b> That is what enforces "a settlement has exactly one asset bank at any time" in the
/// schema rather than in a service that has to remember to check - a second bank for the same settlement cannot be inserted at all.
/// </para>
/// <para>
/// Membership is the thing that moves. A newly connected settlement has its row repointed at the bank it joins; one cut off by a pillaged road gets a row
/// pointing at a fresh empty bank, and when the connection is restored that bank's balances are folded back and it is deleted.
/// </para>
/// </remarks>
public sealed class AssetBankSettlementMembership
{
    public int SettlementId { get; set; }

    public int AssetBankId { get; set; }

    public AssetBank Bank { get; set; } = null!;

    public Settlement Settlement { get; set; } = null!;
}
