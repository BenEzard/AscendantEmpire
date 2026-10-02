using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Stores quantities for a faction scope.
/// A bank may be attached to zero or many settlements via memberships.
/// </summary>
public sealed class AssetBank
{
    public int Id { get; set; }
    public int FactionId { get; set; }

    public AssetBankKind Kind { get; set; }

    /// <summary>
    /// Optional network token to make regrouping easier.
    /// </summary>
    public Guid NetworkKey { get; set; } = Guid.NewGuid();

    public ICollection<AssetBankBalance> Balances { get; set; } = [];
    public ICollection<AssetBankSettlementMembership> SettlementMemberships { get; set; } = [];
}
