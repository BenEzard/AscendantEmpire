namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// One MapResource on the tile the selection could take from, and what taking it would yield them.
/// </summary>
public interface ITakeResourcesOption
{
    /// <summary>The instance to name in the order.</summary>
    int MapResourceInstanceId { get; }

    /// <summary>Which of the tile's two places it holds, so the dialog lists them as the tile draws them.</summary>
    byte Slot { get; }

    /// <summary>The MapResourceType's name, e.g. Berries.</summary>
    string DisplayName { get; }

    /// <summary>The type's tile icon, or null for a type that draws none.</summary>
    string? IconPath { get; }

    /// <summary>
    /// How many whole units of the MapResource the selection would take, bounded by what is actually there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What the Forces would <i>get</i>, not what the tile holds. The true quantity is never disclosed - a player sees only their own fuzzed estimate of it,
    /// drawn on the tile - so the dialog talks about the order's outcome instead. Where the patch holds less than the Forces could carry, this is all of it.
    /// </para>
    /// <para>Zero where something has taken the resource to nothing since the toolbar judged it.</para>
    /// </remarks>
    int UnitsWouldTake { get; }

    /// <summary>
    /// What that many units becomes in cargo, a line per Asset the Player's switches leave on.
    /// </summary>
    /// <remarks>Derived from the type's yield rates, so a resource giving two Assets shows both.</remarks>
    IReadOnlyCollection<ITakeResourcesAssetLine> WouldGain { get; }
}
