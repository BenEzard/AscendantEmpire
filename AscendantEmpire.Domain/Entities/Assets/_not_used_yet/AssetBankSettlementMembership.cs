namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Maps settlements to the current bank they participate in.
/// If two settlements are connected by road, they can point to the same AssetBank.
/// </summary>
public sealed class AssetBankSettlementMembership
{
    public int SettlementId { get; set; }
    public int AssetBankId { get; set; }

    public AssetBank Bank { get; set; } = null!;
}
