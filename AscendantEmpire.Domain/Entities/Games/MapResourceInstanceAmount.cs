namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// How much of one Resource a Distribution instance holds, e.g. the oak in the Forest Wood on tile (4,5). Keyed on the instance and the Resource.
/// </summary>
/// <remarks>
/// The instance's own copy of its scenario's amount, so extracting from one tile never changes another that started from the same scenario. Growth and extraction both
/// work on every amount in proportion, and an amount at 0 gets no share of growth, so a Resource that is gone from a tile never comes back.
/// </remarks>
public class MapResourceInstanceAmount
{
    public int MapResourceInstanceId { get; set; }

    /// <summary>
    /// The Resource (a <c>resources.Resource</c>).
    /// </summary>
    public int ResourceId { get; set; }

    /// <summary>
    /// The Game, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The amount on the tile, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    public long Amount { get; set; }
}
