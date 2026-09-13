using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Battles.Services;

/// <summary>
/// Everything a Battle started by a Force stepping onto an enemy-occupied campaign tile is built from.
/// </summary>
public sealed record BattleEngagement
{
    public required int GameId { get; init; }

    /// <summary>
    /// The Force that stepped onto the tile.
    /// </summary>
    /// <remarks>Must be <b>tracked</b> by the context the Battle is commenced on, with its move already staged, because its starting battle tile is written onto it and saved with the rest.</remarks>
    public required UnitInstance Attacker { get; init; }

    /// <summary>
    /// Every enemy Force already standing on the tile, which may belong to more than one Player.
    /// </summary>
    /// <remarks>Tracked for the same reason as <see cref="Attacker"/>. May not be empty, and may not include a Force on the attacker's own side.</remarks>
    public required IReadOnlyCollection<UnitInstance> Defenders { get; init; }

    /// <summary>The campaign map tile the Battle is fought over.</summary>
    public required int CampaignMapTileId { get; init; }

    /// <summary>The direction the attacker stepped, which decides the battle map edge it starts on.</summary>
    public required MoveDirection StepDirection { get; init; }

    /// <summary>The campaign Round the Battle begins in.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>The attacker's open Turn, which the engagement starting is recorded against.</summary>
    public required int PlayerTurnId { get; init; }
}

/// <summary>
/// Starts a Battle when a Force steps onto a campaign tile held by another Player's Forces: prepares the battle map, places both sides on it, and commences the engagement.
/// </summary>
/// <remarks>
/// <para>
/// The layer above <see cref="IBattleLifecycleWriter"/>. The writer knows how to record an engagement; this knows what an engagement started by a campaign step needs around it - a battle map to
/// fight on, each Unit's starting tile, and the Battle's name and original tile ownership - so that the step handler asks for "attack this tile" and nothing more.
/// </para>
/// <para>
/// As the task that introduced it (COMM-1) specifies, every Force is assumed to see every other, so no detection, ambush or hiding is considered. "Enemy" means any other Player, since there is
/// no alliance model yet.
/// </para>
/// <para>
/// On the battle map itself, every side is given a view of the map (<c>game.PlayerBattleMap</c>), and the attacker - the Player whose Turn it is - is given full vision of it while the defenders
/// start with none. That stands in for the real rule, which gives full vision to a Player whose settled territory (or an ally's) holds the tile, or who has an ambushing Force there.
/// </para>
/// </remarks>
public interface IBattleEngagementService
{
    /// <summary>
    /// Starts a Battle on the engagement's campaign tile, and saves it together with everything the caller has already staged on the context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saves.</b> The battle map is generated first, if the tile has never had one; that runs on its own connection and commits on its own, before anything else is written, and a map left
    /// behind by a Battle that then fails to start is simply reused by the next one. Everything else - every side's rows in <c>game.PlayerBattleMap</c> and the attacker's vision of the map, the
    /// attacker's staged move, every Unit's starting battle tile, the Battle, its roster and the history rows - is saved in one transaction, which this opens when the caller has none and
    /// <see cref="IBattleLifecycleWriter.CommenceAsync"/> joins, so either all of it lands or none of it does.
    /// </para>
    /// <para>
    /// On failure the context should be discarded, exactly as with <see cref="IBattleLifecycleWriter.CommenceAsync"/>.
    /// </para>
    /// </remarks>
    /// <returns>The new Battle's id.</returns>
    Task<ServiceResult<int>> CommenceAsync(AscendantEmpireContext context, BattleEngagement engagement, CancellationToken cancelToken = default);
}
