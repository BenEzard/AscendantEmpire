namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// The single owner of the extraction efficiency modifiers a Player holds because of what they have discovered.
/// </summary>
/// <remarks>
/// <para>
/// A Player's <c>PlayerExtractionEfficiencyModifier</c> rows with <c>SourceType == Technology</c> are a materialised projection of a pure function: every
/// <c>TechnologyExtractionEfficiencyGrant</c> on every technology the Player has Discovered, one row each. This reconciles toward that set rather than firing on
/// the discovery itself, so a re-priced grant corrects every Player who already holds it, a withdrawn grant takes its rows with it, and a new way of becoming
/// Discovered inherits the behaviour for free. It is the same shape as <see cref="IPlayerTechnologySustenanceModifierService"/>, for the same reasons.
/// </para>
/// <para>
/// <b>It reads and writes only rows whose <c>SourceType</c> is Technology.</b> A Specialist's or a Department's rows belong to whatever will own those sources.
/// </para>
/// </remarks>
internal interface IPlayerTechnologyExtractionEfficiencyService
{
    /// <summary>
    /// Brings one Player's technology-sourced extraction efficiency modifiers into line with what their discovered technologies grant.
    /// </summary>
    /// <param name="context">The context to work in, so this can join a caller's unit of work. The caller saves.</param>
    /// <param name="gameId">The Game the Player belongs to, which the rows carry so a game delete can purge them directly.</param>
    /// <returns>How many rows were added, changed or removed.</returns>
    Task<int> ReconcileAsync(Data.AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles every Player in one Game, reading the grant catalogue once rather than once per Player.
    /// </summary>
    /// <param name="context">The context to work in. The caller saves.</param>
    /// <returns>How many rows were added, changed or removed.</returns>
    Task<int> ReconcileGameAsync(Data.AscendantEmpireContext context, int gameId, CancellationToken cancellationToken);
}
