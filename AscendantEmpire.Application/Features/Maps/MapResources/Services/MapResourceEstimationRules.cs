using AscendantEmpire.Application.Helpers.Randomness;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Turns a MapResource instance's true quantity into the single number one Player is shown in its place.
/// </summary>
/// <remarks>
/// <para>
/// Pure and in-memory, for the reason <see cref="MapResourceInstantiationRules"/> is: the error is the whole of this feature, and it must be pinnable
/// without a database behind it. See "Estimated quantity" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Nothing is stored. The error is derived afresh on every read from a key that cannot change — the Game's seed, the instance and the Player — so a Player
/// shown an estimate twice is shown the same one, and cannot average repeated views to find the true quantity.
/// </para>
/// </remarks>
internal static class MapResourceEstimationRules
{
    /// <summary>
    /// What <paramref name="playerId"/> is shown for the instance, or <see langword="null"/> where its type carries no fuzzy range.
    /// </summary>
    /// <remarks>
    /// Null rather than the true quantity for an instance with no factor: a type that was authored without a range has not thereby said its quantities are
    /// known exactly, so the honest answer is that there is no estimate to give rather than one claiming perfect knowledge.
    /// </remarks>
    /// <param name="gameSeed">The Game's <c>GenerationSeed</c>.</param>
    /// <param name="quantity">The instance's true quantity, scaled by <c>ScaledAmount.Scale</c>.</param>
    /// <param name="estimationFuzzyFactor">The instance's own factor, which a Player's modifiers will later narrow for that Player alone.</param>
    internal static long? Estimate(long gameSeed, int instanceId, int playerId, long quantity, decimal? estimationFuzzyFactor)
    {
        if (estimationFuzzyFactor == null)
        {
            return null;
        }

        Random random = GameRandom.Create(gameSeed, RandomPurpose.MapResourceEstimationError, instanceId, playerId);

        // Spread across the whole range rather than only above or below, so an estimate is as likely to flatter the tile as to undersell it.
        decimal error = ((decimal)random.NextDouble() * 2m) - 1m;

        decimal shown = quantity * (1m + (estimationFuzzyFactor.Value * error));

        return Math.Max(0, (long)Math.Round(shown, MidpointRounding.AwayFromZero));
    }
}
