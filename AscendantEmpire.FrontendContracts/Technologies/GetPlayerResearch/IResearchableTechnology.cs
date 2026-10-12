using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch;

/// <summary>
/// One technology a Player could put research into right now, with their own figures rather than the catalogue's.
/// </summary>
public interface IResearchableTechnology
{
    int TechnologyId { get; }

    string Code { get; }

    string DisplayName { get; }

    string? Description { get; }

    string IconPath { get; }

    TechnologyEra Era { get; }

    /// <summary>
    /// How far the Player has got: Available, or Unlocked once the acquisition cost has been settled.
    /// </summary>
    TechnologyStatus Status { get; }

    /// <summary>
    /// This Player's directed-research requirement, or null when this technology does not use the directed bucket.
    /// </summary>
    int? DirectedRequired { get; }

    /// <summary>
    /// This Player's experience requirement, or null when this technology does not use the experience bucket.
    /// </summary>
    int? ExperienceRequired { get; }

    /// <summary>
    /// This Player's diffusion requirement, or null when this technology does not use the diffusion bucket.
    /// </summary>
    int? DiffusionRequired { get; }

    int DirectedPoints { get; }

    int ExperiencePoints { get; }

    int DiffusionPoints { get; }

    /// <summary>
    /// What must be paid to unlock it. Empty means free.
    /// </summary>
    IReadOnlyCollection<ICatalogueAcquisitionCost> AcquisitionCosts { get; }

    /// <summary>
    /// What discovering it unlocks or retires. Empty throughout the seeded catalogue, which authors no grants.
    /// </summary>
    IReadOnlyCollection<ICatalogueGrant> Grants { get; }

    /// <summary>
    /// Which kinds of experience fill its experience bucket.
    /// </summary>
    IReadOnlyCollection<ICatalogueExperienceSource> ExperienceSources { get; }

    /// <summary>
    /// Whether this is the technology the Player's directed research is currently going into.
    /// </summary>
    bool IsCurrentProject { get; }
}
