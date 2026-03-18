using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// The actual resource, e.g. Oak, Marble, Coal, Small Game Meat.
/// </summary>
public sealed class Resource
{
    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, e.g. "oak", "marble"
    /// </summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the file path of the icon.
    /// </summary>
    [MaxLength(500)]
    public string? IconPath { get; set; } = null!;

    /// <summary>
    /// Resource always belongs to a meta category.
    /// </summary>
    public int ResourceMetaCategoryId { get; set; }

    public int ResourceGroupId { get; set; }

    public ResourceGroup Group { get; set; } = null!;

    /// <summary>
    /// If true, this resource can never be treated as a luxury regardless of supply.
    /// </summary>
    public bool NeverLuxury { get; set; }

    /// <summary>
    /// Arbitrary bespoke properties.
    /// Stored as JSON via EF value converter.
    /// Examples: density, spoilage turns, burn value, rarity tier.
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; } = [];

    [MaxLength(2000)]
    public string? DevNote { get; set; } = null!;

    public ResourceMetaCategory MetaCategory { get; set; } = null!;

    public ICollection<FactionResourceState> FactionStates { get; set; } = [];
    public ICollection<FactionResourcePriority> FactionPriorities { get; set; } = [];
    public ICollection<ResourceBankBalance> BankBalances { get; set; } = [];
    public ICollection<WorldResourceSnapshot> WorldSnapshots { get; set; } = [];
}
