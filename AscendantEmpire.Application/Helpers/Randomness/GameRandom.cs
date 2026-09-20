namespace AscendantEmpire.Application.Helpers.Randomness;

/// <summary>
/// Builds the random stream for one roll, derived from the Game's seed rather than drawn from a sequence shared across the Game.
/// </summary>
/// <remarks>
/// <para>
/// A single long-lived <see cref="Random"/> per Game would make every roll depend on how many rolls happened to come before it, which is three separate
/// problems at once. The order Forces are processed in would decide their luck. A process re-run after a failure would produce different results from its
/// first attempt - and the end-of-Turn pipeline is not transactional, so that happens. And adding a roll anywhere would shift every roll after it, changing
/// how an existing Game plays out. Deriving a seed per roll has none of them.
/// </para>
/// <para>
/// The derivation mixes the Game's seed, the <see cref="RandomPurpose"/>, and whatever discriminators identify the individual roll - typically the Round, the
/// Player and the entity. Give it the same inputs and it hands back the same stream, on any machine and in any process.
/// </para>
/// <para>
/// <b>Not for anything that has to be unguessable.</b> The derivation is public and the Game's seed is chosen by whoever created the Game. Use
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> wherever secrecy matters.
/// </para>
/// </remarks>
public static class GameRandom
{
    /// <summary>
    /// A <see cref="Random"/> for one roll, reproducible from its inputs alone.
    /// </summary>
    /// <remarks>
    /// The discriminators must pin the roll down completely. Two rolls given the same Game seed, purpose and discriminators share a stream and so produce the
    /// same values, which is the intended behaviour on a re-run and a bug anywhere else.
    /// </remarks>
    /// <param name="gameSeed">The Game's <c>GenerationSeed</c>.</param>
    /// <param name="purpose">What the roll is for, so unrelated systems never share a stream.</param>
    /// <param name="discriminators">What identifies this roll within the purpose - Round, Player, entity id. Order is significant.</param>
    public static Random Create(long gameSeed, RandomPurpose purpose, params ReadOnlySpan<long> discriminators)
    {
        ulong state = Mix((ulong)gameSeed);

        state = Mix(state ^ (ulong)(long)purpose);

        foreach (long discriminator in discriminators)
        {
            state = Mix(state ^ (ulong)discriminator);
        }

        // Random's seeded constructor takes an int, so the 64 bits are folded down rather than truncated: dropping the high half outright would throw away the
        // Game seed's most significant bits, and two Games whose seeds differed only up there would then roll identically. The sign bit is masked off because a
        // negative seed has a chequered history with Random, and one bit of spread is nothing next to being sure of the behaviour.
        return new Random((int)((state ^ (state >> 32)) & int.MaxValue));
    }

    /// <summary>
    /// The SplitMix64 finalizer: scatters the bits of one value so that inputs differing in a single bit give unrelated outputs.
    /// </summary>
    /// <remarks>
    /// Written out here rather than taken from <see cref="HashCode"/>, which is seeded randomly per process and so gives a different answer every time the
    /// server restarts - the one thing this must not do. The constants are SplitMix64's published ones and must not be altered.
    /// </remarks>
    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;

        return value ^ (value >> 31);
    }
}
