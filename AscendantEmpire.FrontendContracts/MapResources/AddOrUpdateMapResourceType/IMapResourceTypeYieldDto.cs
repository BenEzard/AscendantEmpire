namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// One Asset a MapResourceType gives when extracted.
/// </summary>
public interface IMapResourceTypeYieldDto
{
    /// <summary>
    /// The Asset given. Each Asset appears at most once in a type's yields.
    /// </summary>
    int AssetId { get; }

    /// <summary>
    /// How much of the Asset one unit extracted gives, as a whole number. Above 0.
    /// </summary>
    int AmountPerUnit { get; }

    /// <summary>
    /// Whether Players get this Asset before any mechanic switches it on for them.
    /// </summary>
    bool IsEnabledByDefault { get; }
}
