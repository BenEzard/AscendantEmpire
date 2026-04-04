using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Stores quantities for a faction scope.
/// A bank may be attached to zero or many settlements via memberships.
/// </summary>
public sealed class ResourceBank
{
    public int Id { get; set; }
    public int FactionId { get; set; }

    public ResourceBankKind Kind { get; set; }

    /// <summary>
    /// Optional network token to make regrouping easier.
    /// </summary>
    public Guid NetworkKey { get; set; } = Guid.NewGuid();

    public ICollection<ResourceBankBalance> Balances { get; set; } = [];
    public ICollection<ResourceBankSettlementMembership> SettlementMemberships { get; set; } = [];
}
