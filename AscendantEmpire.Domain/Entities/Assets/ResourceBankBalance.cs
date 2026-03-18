namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Current amount of a resource in a given bank.
/// Whole numbers only.
/// </summary>
public sealed class ResourceBankBalance
{
    public int ResourceBankId { get; set; }
    public int ResourceDefinitionId { get; set; }

    public long Quantity { get; set; }

    public ResourceBank Bank { get; set; } = null!;
    public Resource Resource { get; set; } = null!;
}
