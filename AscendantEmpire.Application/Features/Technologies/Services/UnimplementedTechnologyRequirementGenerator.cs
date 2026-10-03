using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// Stands in for a customisation mode that has not been written yet, and refuses rather than approximating one that has.
/// </summary>
/// <remarks>
/// <para>
/// Registered once per unimplemented mode, so the generator lookup finds something for every <see cref="TechnologyCustomisation"/> member and the failure is
/// an explicit refusal naming the mode, not a "no generator registered" from the selector. The alternative — falling back to Identical — would mean a game
/// quietly playing by rules nobody chose, with nothing in the data to show it.
/// </para>
/// <para>
/// Implementing a mode means replacing its registration with a real generator. Nothing else has to change: the requirement figures are already per Player.
/// </para>
/// <para>
/// What each one owes when it is written:
/// <list type="bullet">
///   <item><description>
///     <see cref="TechnologyCustomisation.RandomisedEqual"/> — randomise per Player, then rebalance <b>per era</b> so every Player's totals land within 1% of
///     each other. Per era rather than on the grand total, or one Player ends up cheap at one end of the tree and ruinous at the other while still netting out
///     "equal".
///   </description></item>
///   <item><description>
///     <see cref="TechnologyCustomisation.FullyRandomised"/> — randomise per Player with no rebalancing, so Players really can be better or worse off.
///   </description></item>
/// </list>
/// Both read <c>GameConfiguration.TechnologyFuzziness</c>, and both should seed their randomness from <c>GameIndex.GenerationSeed</c> so a game stays
/// reproducible.
/// </para>
/// </remarks>
/// <param name="customisation">The mode this instance refuses.</param>
internal sealed class UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation customisation) : ITechnologyRequirementGenerator
{
    public TechnologyCustomisation Customisation => customisation;

    public IReadOnlyCollection<PlayerTechnology> Generate(
        IReadOnlyCollection<Technology> catalogue,
        IReadOnlyCollection<Player> players,
        GameConfiguration configuration)
    {
        throw new NotSupportedException(
            $"Technology customisation '{customisation}' is not implemented. Only '{TechnologyCustomisation.Identical}' can be used to set up a game.");
    }
}
