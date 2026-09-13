using AscendantEmpire.Application.Data;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.BattleMaps.Services;

/// <summary>
/// Writes each Player's view of a battle map - the rows in <c>game.PlayerBattleMap</c> - and the vision that explores it.
/// </summary>
/// <remarks>
/// <para>
/// Works through the caller's context, so every write joins whatever transaction the caller already has open. Both methods run a single set-based SQL statement per Player rather than
/// tracking entities: a 60 x 60 battle map is 3,600 rows for each Player.
/// </para>
/// <para>
/// <b>Writes immediately.</b> Unlike the lifecycle writers' staging methods, nothing here waits for a <c>SaveChanges</c>, so a caller that needs the rows to land or roll back with anything
/// else must open the transaction first.
/// </para>
/// </remarks>
public interface IPlayerBattleMapService
{
    /// <summary>
    /// Gives each of the Players a row for every tile of the battle map they do not already have one for.
    /// </summary>
    /// <remarks>
    /// New rows start unexplored: <c>BiomeTypeId</c> Unknown, <c>AltitudeLevel</c> and both turn columns null. A Player who already has rows from an earlier Battle on the same campaign tile
    /// keeps them as they are, so what they explored then is not forgotten.
    /// </remarks>
    /// <param name="battleMapIndexId">A battle map whose generation is complete; its tiles are what the rows are copied from.</param>
    /// <returns>How many rows were inserted, across all the Players.</returns>
    Task<ServiceResult<int>> InitialiseAsync(AscendantEmpireContext context, int gameId, int battleMapIndexId, IReadOnlyCollection<int> playerIds, CancellationToken cancelToken = default);

    /// <summary>
    /// Explores every tile of the battle map for one Player, as seen during the given Turn.
    /// </summary>
    /// <remarks>
    /// Copies each tile's biome and altitude, stamps <c>LastSeenOnTurnId</c>, and stamps <c>FirstSeenOnTurnId</c> only where it is still null. The Player must already have rows on the map
    /// (see <see cref="InitialiseAsync"/>); one who has none is refused rather than silently granted nothing.
    /// </remarks>
    /// <param name="playerTurnId">The Turn the sighting is recorded against.</param>
    /// <returns>How many rows were updated.</returns>
    Task<ServiceResult<int>> GrantFullVisionAsync(AscendantEmpireContext context, int battleMapIndexId, int playerId, int playerTurnId, CancellationToken cancelToken = default);
}
