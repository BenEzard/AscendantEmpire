namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Maps settlements to the current bank they participate in.
/// If two settlements are connected by road, they can point to the same ResourceBank.
/// </summary>
public sealed class ResourceBankSettlementMembership
{
    public int SettlementId { get; set; }
    public int ResourceBankId { get; set; }

    public ResourceBank Bank { get; set; } = null!;
}
