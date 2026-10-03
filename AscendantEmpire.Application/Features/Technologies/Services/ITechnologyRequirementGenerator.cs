using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// Produces every Player's own technology requirements for one game, once, at setup.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what "technology customisation" means. Because the result is written into each
/// <see cref="PlayerTechnology"/> row rather than recomputed on read, a customisation mode is a change to one of these and to nothing else - no migration, no
/// query rewrites, and no second place where "is this bucket full?" could be decided.
/// </para>
/// <para>
/// One implementation per <see cref="TechnologyCustomisation"/> member, all registered against this interface, and the caller picks by the game's setting.
/// Only <see cref="TechnologyCustomisation.Identical"/> is implemented; the other two throw, which is deliberate - a mode that silently behaved as another
/// would be far worse than one that refuses.
/// </para>
/// <para>
/// The signature takes the whole catalogue and every Player at once because <see cref="TechnologyCustomisation.RandomisedEqual"/> cannot be computed any other
/// way: it has to balance each Player's totals against every other's, per era.
/// </para>
/// </remarks>
internal interface ITechnologyRequirementGenerator
{
    /// <summary>
    /// The customisation mode this generator implements. Unique across every registered generator.
    /// </summary>
    TechnologyCustomisation Customisation { get; }

    /// <summary>
    /// Builds one row per Player per technology, with that Player's own requirement figures.
    /// </summary>
    /// <remarks>
    /// Every row comes back <see cref="Domain.Enums.Technologies.TechnologyStatus.Locked"/>. Which of them are actually available is a question about the
    /// prerequisite graph, and belongs to <see cref="IPlayerTechnologyAvailabilityService"/> rather than here.
    /// </remarks>
    /// <param name="catalogue">Every technology, with its base requirements.</param>
    /// <param name="players">The Players to generate for.</param>
    /// <param name="configuration">The game's settings, for the speed multiplier and fuzziness.</param>
    /// <returns>One row per Player per technology, untracked and unsaved.</returns>
    /// <exception cref="NotSupportedException">This generator's mode is not implemented.</exception>
    IReadOnlyCollection<PlayerTechnology> Generate(
        IReadOnlyCollection<Technology> catalogue,
        IReadOnlyCollection<Player> players,
        GameConfiguration configuration);
}
