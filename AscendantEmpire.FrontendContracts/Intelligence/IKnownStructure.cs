using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.FrontendContracts.Intelligence;

/// <summary>
/// What a Player last saw of a Structure - whose it was, how far built, and whether it still stood - already stripped down to the fidelity that was earned.
/// </summary>
/// <remarks>
/// <para>
/// Drawn as a last-known marker on ground the Player can no longer see, ageing from <see cref="LastSeenRoundNumber"/>. There is no server-side expiry.
/// </para>
/// <para>
/// There is deliberately no row id, for the same reason as <see cref="IKnownForce"/>: <see cref="StructureInstanceId"/> ties sightings together only when it has been earned.
/// </para>
/// </remarks>
public interface IKnownStructure
{
    KnownStructureStatus Status { get; }

    SightingFidelity Fidelity { get; }

    /// <summary>
    /// The tile the Structure stands, or stood, on.
    /// </summary>
    ICoord Location { get; }

    int LastSeenPlayerTurnId { get; }

    int LastSeenRoundNumber { get; }

    int LastSeenTurnSequence { get; }

    /// <summary>
    /// Whether it still stood and was still in view when the Turn of the last sighting ended.
    /// </summary>
    bool StillVisible { get; }

    /// <summary>
    /// What kind of Structure it was. Disclosed from <see cref="SightingFidelity.Type"/>.
    /// </summary>
    int? StructureId { get; }

    /// <summary>
    /// Whose Structure it was when last seen. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? OwnerPlayerId { get; }

    /// <summary>
    /// Which Structure it was. Disclosed from <see cref="SightingFidelity.Owner"/>.
    /// </summary>
    int? StructureInstanceId { get; }

    /// <summary>
    /// How far built it was when last seen, from 0 to 100. Disclosed only at <see cref="SightingFidelity.Detail"/>.
    /// </summary>
    double? CompletionPercent { get; }
}
