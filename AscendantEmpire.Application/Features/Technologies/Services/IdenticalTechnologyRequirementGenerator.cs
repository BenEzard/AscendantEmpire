using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// Gives every Player the same requirements: the catalogue's figures with only the game's speed multiplier applied.
/// </summary>
/// <remarks>
/// <para>
/// Identical figures for every Player are the <i>correct</i> answer for this mode rather than a placeholder standing in for randomisation. The rows are still
/// written per Player, because that is what lets a randomised mode be added without touching anything but its own generator.
/// </para>
/// <para>
/// Fuzziness is ignored here. It exists to make Players differ from one another, which is exactly what this mode is not.
/// </para>
/// </remarks>
internal sealed class IdenticalTechnologyRequirementGenerator : ITechnologyRequirementGenerator
{
    public TechnologyCustomisation Customisation => TechnologyCustomisation.Identical;

    public IReadOnlyCollection<PlayerTechnology> Generate(
        IReadOnlyCollection<Technology> catalogue,
        IReadOnlyCollection<Player> players,
        GameConfiguration configuration)
    {
        List<PlayerTechnology> rows = new(catalogue.Count * players.Count);

        foreach (Player player in players)
        {
            foreach (Technology technology in catalogue)
            {
                rows.Add(new PlayerTechnology
                {
                    PlayerId = player.Id,
                    TechnologyId = technology.Id,
                    GameId = player.GameId,

                    // Locked throughout: which of these are reachable is a question about the prerequisite graph, and the availability reconcile answers it.
                    Status = TechnologyStatus.Locked,

                    DirectedRequired = Scale(technology.DirectedRequired, configuration.TechnologySpeedMultiplierPercent),
                    ExperienceRequired = Scale(technology.ExperienceRequired, configuration.TechnologySpeedMultiplierPercent),
                    DiffusionRequired = Scale(technology.DiffusionRequired, configuration.TechnologySpeedMultiplierPercent),

                    DirectedPoints = 0,
                    ExperiencePoints = 0,
                    DiffusionPoints = 0
                });
            }
        }

        return rows;
    }

    /// <summary>
    /// Applies the speed multiplier to one bucket, preserving an unused bucket's null.
    /// </summary>
    /// <remarks>
    /// Null means the bucket is unused, which is a property of the technology and never randomised, so it has to survive scaling. A multiplier small enough to
    /// round a real requirement to nothing is floored at 1: a bucket in use must always need something, or the technology would complete the instant it
    /// unlocked.
    /// </remarks>
    private static int? Scale(int? baseRequirement, long speedMultiplierPercent)
    {
        if (baseRequirement is null)
        {
            return null;
        }

        long scaled = baseRequirement.Value * speedMultiplierPercent / ScaledPercent.Scale;

        return (int)Math.Max(scaled, 1);
    }
}
