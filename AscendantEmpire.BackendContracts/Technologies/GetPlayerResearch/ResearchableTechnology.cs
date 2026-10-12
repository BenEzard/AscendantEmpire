using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies.GetPlayerResearch;

/// <summary>
/// One technology a Player could put research into right now, with their own figures rather than the catalogue's.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="TechnologyStatus.Available"/> and <see cref="TechnologyStatus.Unlocked"/> reach this shape. Locked is excluded because its prerequisites
/// are unmet, and Discovered because there is nothing left to research - neither is a thing a Player can choose, and a list that carried them would have to
/// be filtered again by every client.
/// </para>
/// <para>
/// Self-contained on purpose. The catalogue read serves a few hundred technologies with four child collections each, and a client should not have to pull
/// all of it to label the handful it can actually research, so the display fields travel with the Player's figures.
/// </para>
/// <para>
/// The three requirement figures are <b>this Player's</b>, off their PlayerTechnology row, never the catalogue's base numbers. A null requirement means the
/// technology does not use that bucket at all - it is not a requirement of zero, and anything rendering it has to keep the two apart.
/// </para>
/// </remarks>
public record ResearchableTechnology
{
    public required int TechnologyId { get; init; }

    /// <summary>
    /// The technology's unique code.
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string IconPath { get; init; }

    public required TechnologyEra Era { get; init; }

    /// <summary>
    /// How far the Player has got: Available, or Unlocked once the acquisition cost has been settled.
    /// </summary>
    public required TechnologyStatus Status { get; init; }

    /// <summary>
    /// This Player's directed-research requirement, or null when this technology does not use the directed bucket.
    /// </summary>
    public int? DirectedRequired { get; init; }

    /// <summary>
    /// This Player's experience requirement, or null when this technology does not use the experience bucket.
    /// </summary>
    public int? ExperienceRequired { get; init; }

    /// <summary>
    /// This Player's diffusion requirement, or null when this technology does not use the diffusion bucket.
    /// </summary>
    public int? DiffusionRequired { get; init; }

    public required int DirectedPoints { get; init; }

    public required int ExperiencePoints { get; init; }

    public required int DiffusionPoints { get; init; }

    /// <summary>
    /// What must be paid to unlock it. Empty means free, and a free technology unlocks the moment it is chosen.
    /// </summary>
    public required IReadOnlyCollection<CatalogueAcquisitionCost> AcquisitionCosts { get; init; }

    /// <summary>
    /// What discovering it unlocks or retires, with each target resolved to a code and display name.
    /// </summary>
    /// <remarks>
    /// Empty for every technology in the seeded catalogue, which authors no grants at all - what a technology gives is carried as text in its
    /// <see cref="Description"/> for now. Served regardless, because the shape is what the catalogue will fill rather than something to add later.
    /// </remarks>
    public required IReadOnlyCollection<CatalogueGrant> Grants { get; init; }

    /// <summary>
    /// Which kinds of experience fill its experience bucket.
    /// </summary>
    /// <remarks>An experience requirement with no sources at all can never fill, which is worth showing rather than hiding.</remarks>
    public required IReadOnlyCollection<CatalogueExperienceSource> ExperienceSources { get; init; }

    /// <summary>
    /// Whether this is the technology the Player's directed research is currently going into.
    /// </summary>
    /// <remarks>
    /// Derived from PlayerResearchState rather than stored on the row, and carried here so a client can mark the current project without comparing ids
    /// against another part of the payload.
    /// </remarks>
    public required bool IsCurrentProject { get; init; }
}
