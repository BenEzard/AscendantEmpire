using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// A batch of "who could see this tile at this moment" questions about one Turn.
/// </summary>
/// <remarks>
/// Batched because a fold asks about a whole Turn at once, and the answers share almost all their work: the Observers near a Force's path, and the
/// terrain around them, are loaded once for every step of it.
/// </remarks>
internal sealed record WitnessQuery
{
    public required int GameId { get; init; }

    /// <summary>
    /// The Turn the probed events happened in. Observers destroyed during it are counted for the sequences before their destruction.
    /// </summary>
    public required int PlayerTurnId { get; init; }

    /// <summary>
    /// Players whose coalitions can never be witnesses to these events — normally the acting Player.
    /// </summary>
    /// <remarks>
    /// Widened to each Player's whole coalition. The acting side gets no report of what it saw itself, since its client already showed it, and an ally
    /// sharing its vision saw it on the same screen.
    /// </remarks>
    public required IReadOnlyCollection<int> ExcludedPlayerIds { get; init; }

    public required IReadOnlyCollection<WitnessProbe> Probes { get; init; }
}

/// <summary>
/// One question for the witness service: which coalitions could see this tile at this point in the Turn.
/// </summary>
internal sealed record WitnessProbe
{
    /// <summary>
    /// The caller's handle for the answer, unique within a query — for a movement fold, naturally the <c>MovementHistory</c> row id.
    /// </summary>
    public required long Key { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// When in the Turn the event happened, from the Turn's sequence counter.
    /// </summary>
    public required int TurnSequence { get; init; }

    /// <summary>
    /// The Force the probe is about, or null when the question is about the tile itself — a Structure, or an engagement.
    /// </summary>
    /// <remarks>
    /// A tile is always seen at full fidelity; a Force goes through <see cref="SightingFidelityRules.ForForce"/>.
    /// </remarks>
    public ForceSubject? Force { get; init; }
}

/// <summary>
/// The Force a <see cref="WitnessProbe"/> is asking about, as it was at the probed moment.
/// </summary>
/// <remarks>
/// Taken from the event rather than from the Force's row, which may no longer exist by the time the fold runs.
/// </remarks>
internal sealed record ForceSubject
{
    public required int UnitInstanceId { get; init; }

    public required int UnitTemplateId { get; init; }

    public int? HealthyTroops { get; init; }
}
