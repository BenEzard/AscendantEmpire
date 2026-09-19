namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// One Resource a MapResourceType gives when extracted.
/// </summary>
public interface IMapResourceTypeYieldDto
{
    /// <summary>
    /// The Resource given. Each Resource appears at most once in a type's yields.
    /// </summary>
    int ResourceId { get; }

    /// <summary>
    /// How much of the Resource one unit extracted gives, as a whole number. Above 0.
    /// </summary>
    int AmountPerUnit { get; }

    /// <summary>
    /// Whether Players get this Resource before any mechanic switches it on for them.
    /// </summary>
    bool IsEnabledByDefault { get; }
}
