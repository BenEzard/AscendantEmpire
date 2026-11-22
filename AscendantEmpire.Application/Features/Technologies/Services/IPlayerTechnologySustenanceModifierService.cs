namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// The single owner of the sustenance modifiers a Player holds because of what they have discovered.
/// </summary>
/// <remarks>
/// <para>
/// A Player's <c>PlayerSustenanceModifier</c> rows with <c>SourceType == Technology</c> are a materialised projection of a pure function: every
/// <c>TechnologySustenanceModifierGrant</c> on every technology the Player has Discovered, one row each. This reconciles toward that set rather than firing
/// on the discovery itself, and the difference matters in three places. A re-priced grant corrects the rows of every Player who already holds it. A grant
/// withdrawn from the catalogue takes its rows with it. And a <b>new</b> way of becoming Discovered - diffusion, a scenario start, a cheat - inherits the
/// behaviour for free, because the discovery path is not where the work lives.
/// </para>
/// <para>
/// Call it after any discovery, and after any seed that touched the technology catalogue. Being convergent is what makes it safe to call freely and makes
/// every race on it benign.
/// </para>
/// <para>
/// <b>It reads and writes only rows whose <c>SourceType</c> is Technology.</b> A Specialist's or a Department's rows belong to whatever will own those
/// sources, and a reconcile that wanted none would otherwise delete them.
/// </para>
/// </remarks>
internal interface IPlayerTechnologySustenanceModifierService
{
    /// <summary>
    /// Brings one Player's technology-sourced sustenance modifiers into line with what their discovered technologies grant.
    /// </summary>
    /// <param name="context">The context to work in, so this can join a caller's unit of work. The caller saves.</param>
    /// <param name="gameId">The Game the Player belongs to, which the rows carry so a game delete can purge them directly.</param>
    /// <param name="playerId">The Player to reconcile.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>How many rows were added, changed or removed.</returns>
    Task<int> ReconcileAsync(
        Data.AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles every Player in one Game, reading the grant catalogue once rather than once per Player.
    /// </summary>
    /// <param name="context">The context to work in. The caller saves.</param>
    /// <param name="gameId">The Game whose Players to reconcile.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>How many rows were added, changed or removed.</returns>
    Task<int> ReconcileGameAsync(
        Data.AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken);
}
