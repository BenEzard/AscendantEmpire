using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Current amount of an asset in a given bank.
/// Whole numbers only.
/// </summary>
public sealed class AssetBankBalance
{
    public int AssetBankId { get; set; }
    public int AssetId { get; set; }

    public long Quantity { get; set; }

    public AssetBank Bank { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
