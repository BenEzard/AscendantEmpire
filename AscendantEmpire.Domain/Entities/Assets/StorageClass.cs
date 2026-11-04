using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// How an Asset is kept, as against what it is - the storage yard, the armoury, the granary.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one per Asset and required, which is why this is a catalogue behind a non-null foreign key rather than a tag: a join table cannot express
/// "exactly one", and capacity enforcement reads it on every credit.
/// </para>
/// <para>
/// Storage deliberately cuts across the display hierarchy, so a class is free to span it. The storage yard covers both Wood and Stone - two categories and
/// four groups - while the fuel dump covers four rows out of the middle of the flat Raw Materials category. Declaring capacity against MetaCategory,
/// Category or Group nodes instead would force the display taxonomy to be reshaped for storage reasons.
/// </para>
/// <para>There is no nesting: a cap binds against one class, and a refusal names that one.</para>
/// </remarks>
public sealed class StorageClass
{
    /// <summary>
    /// A single word rather than a dotted path, unlike <see cref="Asset.Code"/> - the catalogue is flat, so there is no parent prefix to carry.
    /// </summary>
    public const int MAX_CODE_LENGTH = 50;

    public const int MAX_DISPLAY_NAME_LENGTH = 50;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, e.g. "Armoury". What every upsert matches on.
    /// </summary>
    /// <remarks>
    /// Exposed as a constant by the storage class seed data, which the Asset seed names rather than spelling the string out, so a divergence between the two
    /// is a compile error instead of a seed-time resolution failure.
    /// </remarks>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    /// <summary>
    /// The name a capacity refusal uses, e.g. "Armoury".
    /// </summary>
    /// <remarks>Unique, because a refusal naming the class that bound would be unexplainable if two classes read alike.</remarks>
    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// The Assets kept this way.
    /// </summary>
    /// <remarks>The inverse the Asset configuration's foreign key is declared against.</remarks>
    public ICollection<Asset> Assets { get; set; } = [];
}
