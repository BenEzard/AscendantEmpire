using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.ForceOrders.StepForce;

/// <summary>
/// The body of a single-tile step: which Force moves, and which way.
/// </summary>
/// <remarks>
/// <para>
/// The game and player are deliberately absent — they come from the caller's game token, so a step cannot be aimed at another player's Forces.
/// </para>
/// <para>
/// A direction rather than a destination coordinate. The server already knows where the Force stands, so letting it work out the neighbour keeps
/// the wrapping arithmetic in one place and makes it impossible for a stale client view to move a Force somewhere it was never standing next to.
/// </para>
/// </remarks>
public interface IStepForceDto
{
    /// <summary>
    /// Which Force is stepping. A Force is a category rather than a type, so this is a <c>UnitInstance</c> id today; see <c>docs/Terminology.md</c>.
    /// </summary>
    int UnitInstanceId { get; }

    /// <summary>Which of the eight neighbouring tiles to step onto. Serialised as the enum member's name, e.g. <c>NorthEast</c>.</summary>
    MoveDirection Direction { get; }
}
