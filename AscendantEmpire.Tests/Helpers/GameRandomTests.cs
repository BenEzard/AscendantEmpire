using AscendantEmpire.Application.Helpers.Randomness;

namespace AscendantEmpire.Tests.Helpers;

/// <summary>
/// Tests for <see cref="GameRandom"/>: that a roll derived from a Game's seed is reproducible, and that unrelated rolls do not share a stream.
/// </summary>
/// <remarks>
/// No database - the derivation is pure arithmetic.
/// </remarks>
[TestFixture]
internal class GameRandomTests
{
    private const long _GAME_SEED = 1234567890123456789;

    /// <summary>
    /// The dead share the pinned inputs draw today. Only ever updated deliberately, alongside a decision that Games already in progress may roll differently.
    /// </summary>
    private const int _PINNED_DEAD_SHARE = 200;

    [Test, Description("The same inputs give the same values, which is what lets a re-run of the end-of-Turn pipeline repeat its casualties exactly.")]
    public void ShouldReproduceTheSameStreamFromTheSameInputs()
    {
        int[] first = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 12));
        int[] second = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 12));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test, Description("Two Forces in the same Round roll independently, so neither the order they are processed in nor one another's luck can affect them.")]
    public void ShouldGiveDifferentStreamsToDifferentEntities()
    {
        int[] firstForce = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 12));
        int[] secondForce = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 13));

        Assert.That(secondForce, Is.Not.EqualTo(firstForce));
    }

    [Test, Description("The same Force rolls differently from one Round to the next, so a starving Force does not take identical casualties every Turn.")]
    public void ShouldGiveDifferentStreamsToDifferentRounds()
    {
        int[] firstRound = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 12));
        int[] secondRound = Draw(GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 5, 7, 12));

        Assert.That(secondRound, Is.Not.EqualTo(firstRound));
    }

    [Test, Description("Two Games differing only in the high bits of their seed roll differently, pinning that the 64-bit seed is folded rather than truncated.")]
    public void ShouldUseTheWholeGameSeed()
    {
        int[] low = Draw(GameRandom.Create(1L, RandomPurpose.StarvationCasualties, 4, 7, 12));
        int[] high = Draw(GameRandom.Create(1L << 40, RandomPurpose.StarvationCasualties, 4, 7, 12));

        Assert.That(high, Is.Not.EqualTo(low));
    }

    [Test, Description("A negative Game seed is accepted, which matters because the server generates seeds across the whole range of a long.")]
    public void ShouldAcceptANegativeGameSeed()
    {
        Assert.That(() => GameRandom.Create(long.MinValue, RandomPurpose.StarvationCasualties, 4, 7, 12).Next(), Throws.Nothing);
    }

    [Test, Description("The derivation is pinned to an exact value: changing it would silently re-roll every Turn of every Game already in progress.")]
    public void ShouldNotChangeTheDerivation()
    {
        Random random = GameRandom.Create(_GAME_SEED, RandomPurpose.StarvationCasualties, 4, 7, 12);

        Assert.That(random.Next(100, 301), Is.EqualTo(_PINNED_DEAD_SHARE));
    }

    private static int[] Draw(Random random)
    {
        return [.. Enumerable.Range(0, 10).Select(_ => random.Next(1_000))];
    }
}
