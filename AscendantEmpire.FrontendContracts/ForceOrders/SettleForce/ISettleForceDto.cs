using AscendantEmpire.Domain.Enums.Settlements;

namespace AscendantEmpire.FrontendContracts.ForceOrders.SettleForce;

/// <summary>
/// The body of a settle order: which Force is consumed, what it founds, and what the player has called it.
/// </summary>
/// <remarks>
/// <para>
/// The game and player are deliberately absent — they come from the caller's game token, so a settle order cannot be aimed at another player's Forces.
/// </para>
/// <para>
/// The tile is absent too, for the same reason a step takes a direction rather than a destination: the server already knows where the Force stands, and that is where the
/// settlement goes.
/// </para>
/// </remarks>
public interface ISettleForceDto
{
    /// <summary>
    /// Which Force is settling. A Force is a category rather than a type, so this is a <c>UnitInstance</c> id today; see <c>docs/Terminology.md</c>.
    /// </summary>
    int UnitInstanceId { get; }

    /// <summary>
    /// Which tier of settlement to found. Serialised as the enum member's name, e.g. <c>Village</c>.
    /// </summary>
    /// <remarks>
    /// Only <c>Camp</c> and <c>Village</c> can be founded; the higher tiers are reached by growth. The tier decides which toolbar action's rules the order is judged by, so
    /// it is sent rather than inferred from the Force.
    /// </remarks>
    SettlementTier Tier { get; }

    /// <summary>
    /// The player's name for the settlement. Trimmed by the server, and unique within the game.
    /// </summary>
    /// <remarks>
    /// Required for a tier the player names and ignored for one they do not: a camp is unnamed, so the client sends nothing for one and anything it does send is
    /// dropped rather than stored.
    /// </remarks>
    string? Name { get; }
}
