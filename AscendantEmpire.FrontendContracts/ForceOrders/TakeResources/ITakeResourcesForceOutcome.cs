namespace AscendantEmpire.FrontendContracts.ForceOrders.TakeResources;

/// <summary>
/// What one Force in a take-resources order came away with.
/// </summary>
public interface ITakeResourcesForceOutcome
{
    /// <summary>
    /// The Force. A Force is a category rather than a type, so this is a <c>UnitInstance</c> id today; see <c>docs/Terminology.md</c>.
    /// </summary>
    int UnitInstanceId { get; }

    /// <summary>The Force's name, so the player is told who rather than which id.</summary>
    string DisplayName { get; }

    /// <summary>How many whole units this Force took.</summary>
    /// <remarks>Zero where the Forces ahead of it had already emptied the MapResource. Such a Force kept its movement: it did nothing.</remarks>
    int UnitsTaken { get; }
}
