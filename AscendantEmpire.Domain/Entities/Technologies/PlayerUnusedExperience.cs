using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// Experience of one kind that one Player earned but had nowhere to put.
/// </summary>
/// <remarks>
/// <para>
/// A dead-end counter. These points are <b>never spent and never applied to a Technology discovered later</b> - they accumulate only so that Players can be
/// compared on them. Composite primary key on (PlayerId, ExperienceTypeId).
/// </para>
/// <para>
/// A type's points land here when no Technology of the Player's consumes it, when every Technology that does is Locked or already Discovered, from the
/// remainder of an even split that did not divide cleanly, and from the surplus when a recipient was clamped at its requirement.
/// </para>
/// <para>
/// <b>Only unused points are recorded</b> - there is deliberately no lifetime-earned column, so this answers "who wasted the most Jungle experience", not "who
/// has done the most in jungle".
/// </para>
/// <para>
/// This is why <see cref="PlayerResearchState"/> has no UnspentExperiencePoints column: unallocated experience is typed, and a single scalar could not express
/// "400 unusable Jungle points and 20 unusable Mining points".
/// </para>
/// </remarks>
public class PlayerUnusedExperience
{
    public int PlayerId { get; set; }

    public int ExperienceTypeId { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.</remarks>
    public int GameId { get; set; }

    /// <summary>
    /// Running total of points that could not be used, for the life of the game.
    /// </summary>
    public int Points { get; set; }

    public Player Player { get; set; } = null!;

    public ExperienceType ExperienceType { get; set; } = null!;
}
