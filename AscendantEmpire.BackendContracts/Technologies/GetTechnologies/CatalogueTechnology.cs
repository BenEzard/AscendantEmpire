using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One technology in the catalogue, with everything hanging off it.
/// </summary>
/// <remarks>
/// <para>
/// The whole catalogue in one shape, rather than separate calls per child collection: the tech tree cannot be drawn without the prerequisite edges, and an
/// editor cannot show a technology without what it costs and what it gives. Player-independent while technology customisation is Identical, so a client may
/// cache it for the life of a game.
/// </para>
/// <para>
/// The three requirement figures are the <b>catalogue's</b> base numbers, not any Player's. A Player's own requirements live on their PlayerTechnology row and
/// are not served from here.
/// </para>
/// </remarks>
public record CatalogueTechnology
{
    public required int Id { get; init; }

    /// <summary>
    /// The technology's unique code, which prerequisites and the starter list reference it by.
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required string IconPath { get; init; }

    /// <summary>
    /// The age of the tree this technology sits in.
    /// </summary>
    /// <remarks>Read only by the randomised requirement generator, so it is served for the editor rather than because a client should group by it.</remarks>
    public required TechnologyEra Era { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>
    /// Authored position in the tech-tree diagram, or null to let the client lay the technology out itself.
    /// </summary>
    public int? TreeColumn { get; init; }

    /// <summary>
    /// Authored position in the tech-tree diagram, or null to let the client lay the technology out itself.
    /// </summary>
    public int? TreeRow { get; init; }

    /// <summary>
    /// Base directed-research points required, or null when this technology does not use the directed bucket.
    /// </summary>
    public int? DirectedRequired { get; init; }

    /// <summary>
    /// Base experience points required, or null when this technology does not use the experience bucket.
    /// </summary>
    public int? ExperienceRequired { get; init; }

    /// <summary>
    /// Base diffusion points required, or null when this technology does not use the diffusion bucket.
    /// </summary>
    public int? DiffusionRequired { get; init; }

    /// <summary>
    /// What must be paid to unlock it. Empty means free.
    /// </summary>
    public required IReadOnlyCollection<CatalogueAcquisitionCost> AcquisitionCosts { get; init; }

    /// <summary>
    /// What must be discovered first, in display-name order.
    /// </summary>
    public required IReadOnlyCollection<CataloguePrerequisite> Prerequisites { get; init; }

    /// <summary>
    /// What discovering it unlocks or retires.
    /// </summary>
    public required IReadOnlyCollection<CatalogueGrant> Grants { get; init; }

    /// <summary>
    /// Which kinds of experience fill its experience bucket.
    /// </summary>
    public required IReadOnlyCollection<CatalogueExperienceSource> ExperienceSources { get; init; }
}
