using AscendantEmpire.Application.Features.Maps.MapResources.Services;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins the estimate a Player is shown in place of a MapResource instance's true quantity.
/// </summary>
/// <remarks>
/// No database: the estimate is pure, and the whole point of it is that it is reproducible from the Game's seed and never re-rolled, which can only be
/// proved by calling the rules directly.
/// </remarks>
[TestFixture]
internal sealed class MapResourceEstimationRulesTests
{
    private const long _SEED = 20261108;

    private const int _INSTANCE_ID = 5_501;
    private const int _PLAYER_ID = 42;

    private const long _QUANTITY = 1_000_000;

    private const decimal _FUZZY_FACTOR = 0.200m;

    /// <summary>
    /// How many Players the spread tests draw over. Enough that identical values across all of them cannot be coincidence.
    /// </summary>
    private const int _PLAYER_SAMPLE = 20;

    #region Reproducibility

    [Test, Description("The same seed, instance and Player give the same estimate every time, so a Player cannot average repeated views to find the truth.")]
    public void ShouldGiveTheSameEstimateForTheSameSeedInstanceAndPlayer()
    {
        long? first = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, _PLAYER_ID, _QUANTITY, _FUZZY_FACTOR);
        long? second = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, _PLAYER_ID, _QUANTITY, _FUZZY_FACTOR);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test, Description("Two Players looking at one instance are shown different estimates, so the error is per Player rather than per instance.")]
    public void ShouldEstimateDifferentlyForDifferentPlayers()
    {
        List<long?> estimates = [];

        for (int playerId = 1; playerId <= _PLAYER_SAMPLE; playerId++)
        {
            estimates.Add(MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, playerId, _QUANTITY, _FUZZY_FACTOR));
        }

        Assert.That(estimates.Distinct().Count(), Is.GreaterThan(1));
    }

    [Test, Description("One Player looking at two instances is shown different estimates, so the error is per instance rather than per Player.")]
    public void ShouldEstimateDifferentlyForDifferentInstances()
    {
        List<long?> estimates = [];

        for (int instanceId = 1; instanceId <= _PLAYER_SAMPLE; instanceId++)
        {
            estimates.Add(MapResourceEstimationRules.Estimate(_SEED, instanceId, _PLAYER_ID, _QUANTITY, _FUZZY_FACTOR));
        }

        Assert.That(estimates.Distinct().Count(), Is.GreaterThan(1));
    }
    #endregion

    #region Bounds

    [Test, Description("An estimate never strays further from the quantity than the instance's fuzzy factor allows.")]
    public void ShouldKeepEveryEstimateWithinTheFuzzyFactor()
    {
        var widest = (long)(_QUANTITY * _FUZZY_FACTOR);

        for (int playerId = 1; playerId <= _PLAYER_SAMPLE; playerId++)
        {
            long? estimate = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, playerId, _QUANTITY, _FUZZY_FACTOR);

            Assert.That(estimate, Is.InRange(_QUANTITY - widest, _QUANTITY + widest));
        }
    }

    [Test, Description("An estimate is never negative, however wide the fuzzy factor, because a negative quantity would be nonsense on the tile.")]
    public void ShouldNeverEstimateBelowZero()
    {
        const decimal wholeRange = 1.000m;

        for (int playerId = 1; playerId <= _PLAYER_SAMPLE; playerId++)
        {
            long? estimate = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, playerId, _QUANTITY, wholeRange);

            Assert.That(estimate, Is.GreaterThanOrEqualTo(0));
        }
    }

    [Test, Description("An exhausted instance estimates at zero rather than drifting either side of it.")]
    public void ShouldEstimateZeroForAnExhaustedInstance()
    {
        long? estimate = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, _PLAYER_ID, quantity: 0, _FUZZY_FACTOR);

        Assert.That(estimate, Is.EqualTo(0));
    }
    #endregion

    #region No factor to estimate from

    [Test, Description("A type carrying no fuzzy range yields no estimate, since null does not claim the quantity is known exactly.")]
    public void ShouldGiveNoEstimateWhereTheInstanceHasNoFuzzyFactor()
    {
        long? estimate = MapResourceEstimationRules.Estimate(_SEED, _INSTANCE_ID, _PLAYER_ID, _QUANTITY, estimationFuzzyFactor: null);

        Assert.That(estimate, Is.Null);
    }
    #endregion
}
