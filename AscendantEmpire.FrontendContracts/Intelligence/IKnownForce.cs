using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// Where a Player last saw a Force, and in what state, already stripped down to the fidelity that was earned.
/// </summary>
/// <remarks>
/// <para>
/// Drawn as a last-known-position marker that ages from <see cref="LastSeenRoundNumber"/>. There is no server-side expiry.
/// </para>
/// <para>
/// There is deliberately no row id. A stable id would tie two sightings of the same Force together even where fidelity did not earn that; <see cref="UnitInstanceId"/> does so only when it has been
/// earned.
/// </para>
/// </remarks>
public interface IKnownForce
{
    KnownForceStatus Status { get; }

    SightingFidelity Fidelity { get; }

    ICoord LastSeenLocation { get; }

    int LastSeenPlayerTurnId { get; }

    int LastSeenRoundNumber { get; }

    int LastSeenTurnSequence { get; }

    /// <summary>
    /// The direction the Force was last seen leaving in, or null when it was still in view or destroyed.
    /// </summary>
    MoveDirection? ExitDirection { get; }

    bool StillVisible { get; }

    /// <summary>
    /// What kind of Force it was. Disclosed from <see cref="SightingFidelity.Type"/>.
    /// </summary>
    int? UnitTemplateId { get; }

    /// <summary>
    /// Whose Force it was. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? OwnerPlayerId { get; }

    /// <summary>
    /// Which Force it was. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? UnitInstanceId { get; }

    /// <summary>
    /// Its unwounded soldiers. Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </summary>
    int? HealthyTroops { get; }
}
