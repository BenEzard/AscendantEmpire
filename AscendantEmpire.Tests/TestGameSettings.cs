using AscendantEmpire.Application.Options;

using Microsoft.Extensions.Options;

namespace AscendantEmpire.Tests;

/// <summary>
/// Builds the <see cref="GameSettings"/> options that game-setup tests run against.
/// </summary>
/// <remarks>
/// <para>
/// Shared rather than rebuilt per fixture because <see cref="GameSettings"/> carries required nested sections: a fixture that filled in only the
/// two values it cared about would not compile, and one that filled them in badly would fail inside the service rather than in the test.
/// </para>
/// <para>
/// The values are the test project's own, deliberately not read from any appsettings file. A test that pins behaviour at the minimum or maximum
/// player count says so against these constants, so retuning the shipped configuration cannot quietly turn a boundary test into a mid-range one.
/// </para>
/// </remarks>
public static class TestGameSettings
{
    public const int MINIMUM_PLAYERS_PER_GAME = 2;
    public const int MAXIMUM_PLAYERS_PER_GAME = 10;

    public const int MINIMUM_BATTLE_MAP_SIZE = 10;
    public const int MAXIMUM_BATTLE_MAP_SIZE = 50;
    public const int DEFAULT_BATTLE_MAP_SIZE = 50;

    public const int BASE_RESEARCH_POINTS = 100;
    public const long TECHNOLOGY_SPEED_MULTIPLIER_PERCENT = 1_000;
    public const long TECHNOLOGY_FUZZINESS_PERCENT = 0;

    /// <summary>
    /// The settings as every game-setup test sees them, with the player bounds overridable for a test that needs its own.
    /// </summary>
    public static IOptions<GameSettings> Create(int minimumPlayers = MINIMUM_PLAYERS_PER_GAME, int maximumPlayers = MAXIMUM_PLAYERS_PER_GAME)
    {
        return Options.Create(new GameSettings
        {
            MinimumPlayersPerGame = minimumPlayers,
            MaximumPlayersPerGame = maximumPlayers,
            BattleMapSettings = new BattleMapSettings
            {
                MinimumBattleMapSize = MINIMUM_BATTLE_MAP_SIZE,
                MaximumBattleMapSize = MAXIMUM_BATTLE_MAP_SIZE,
                DefaultBattleMapSize = DEFAULT_BATTLE_MAP_SIZE
            },
            TechnologySettings = new TechnologySettings
            {
                BaseResearchPoints = BASE_RESEARCH_POINTS,
                TechnologySpeedMultiplierPercent = TECHNOLOGY_SPEED_MULTIPLIER_PERCENT,
                TechnologyFuzzinessPercent = TECHNOLOGY_FUZZINESS_PERCENT
            }
        });
    }
}
