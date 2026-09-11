using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Everything that may one day decide how well an Observer made out a Force it saw.
/// </summary>
/// <remarks>
/// Carried in full now, although <see cref="SightingFidelityRules.ForForce"/> reads none of it yet, so filling in the rule later changes the rule and not
/// every caller that builds these.
/// </remarks>
internal sealed record ForceSightingInputs
{
    /// <summary>
    /// The Turn the sighting happened in. With <see cref="TurnSequence"/>, the observer and the subject, what a random component must be seeded from.
    /// </summary>
    public required int PlayerTurnId { get; init; }

    public required int TurnSequence { get; init; }

    public required int ObserverId { get; init; }

    public required RecordType ObserverRecordType { get; init; }

    /// <summary>
    /// The Observer's Detection, fixed-point at Scale 1,000.
    /// </summary>
    public required long ObserverDetection { get; init; }

    /// <summary>
    /// The unwounded soldiers watching, or null for a Structure or a Unit known only from its history.
    /// </summary>
    public int? ObserverHealthyTroops { get; init; }

    /// <summary>
    /// How many tiles separate the Observer from the Force, Chebyshev and wrap-aware.
    /// </summary>
    public required int Distance { get; init; }

    public required int SubjectUnitInstanceId { get; init; }

    /// <summary>
    /// The template the Force was built from, which is where its design composition is read.
    /// </summary>
    public required int SubjectUnitTemplateId { get; init; }

    /// <summary>
    /// The Force's Visibility — how easily it is spotted — fixed-point at Scale 1,000.
    /// </summary>
    public required long SubjectVisibility { get; init; }

    public int? SubjectHealthyTroops { get; init; }

    public required BiomeType SubjectBiomeType { get; init; }

    public required CampaignMapGeographyType SubjectGeographyType { get; init; }
}

/// <summary>
/// How much an Observer learns from a sighting.
/// </summary>
/// <remarks>
/// <para>
/// Seeing a <b>tile</b> — its terrain and the Structures standing on it — is always full fidelity. Seeing a <b>Force</b> on it is a judgement, and that
/// judgement is not designed yet: <see cref="ForForce"/> is a deliberate stub.
/// </para>
/// <para>
/// Fidelity is a disclosure rule. What it returns is recorded alongside the truth, and a read projection strips identity back to it. See "Fidelity" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// </remarks>
internal static class SightingFidelityRules
{
    /// <summary>
    /// The Detection a Structure brings to a sighting, fixed-point at Scale 1,000.
    /// </summary>
    /// <remarks>
    /// A stand-in. <c>Structure</c> has a Sight range and no Detection stat, so every Structure watches as well as this. Worth about as much as a trained
    /// scout, which is roughly what a watchtower should be. Replace with a per-Structure value when one exists.
    /// </remarks>
    internal const long STRUCTURE_DETECTION = 2000;

    /// <summary>
    /// The fidelity of seeing a tile and the Structures on it.
    /// </summary>
    /// <remarks>
    /// Always <see cref="SightingFidelity.Detail"/>. Ground and buildings do not hide from an Observer that can see them; that is what Sight already
    /// decides.
    /// </remarks>
    internal static SightingFidelity ForTile()
    {
        return SightingFidelity.Detail;
    }

    /// <summary>
    /// The fidelity of seeing a Force.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stubbed to <see cref="SightingFidelity.Detail"/></b>, which is what the live map read already shows of any Force in sight, so intelligence
    /// discloses nothing live vision does not.
    /// </para>
    /// <para>
    /// Being in sight at all always earns at least <see cref="SightingFidelity.Movement"/>; that floor is not expected to change.
    /// </para>
    /// </remarks>
    internal static SightingFidelity ForForce(ForceSightingInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        // TODO: F-24 — the real curve. It is expected to weigh:
        //   - the number of troops, on both sides (more eyes see more; a larger Force is harder to hide);
        //   - the Force's unit design composition, read through its template (starting with TotalVisibility);
        //   - the Biome and Geography of the tile the Force is standing on (cover);
        //   - the Observer's Detection, and probably its distance;
        //   - a random component. That must be deterministic - seeded from the Turn, the sequence, the Observer and the subject - so that re-running a
        //     fold can never change a sighting that has already been reported.
        return SightingFidelity.Detail;
    }
}
