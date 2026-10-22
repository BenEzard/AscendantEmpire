namespace AscendantEmpire.Domain.Enums.Assets;

/// <summary>
/// Whose people, or whose things, the Assets under an <c>AssetCategory</c> are counted for.
/// </summary>
/// <remarks>
/// <para>
/// Almost every Asset has only one Player attached to it - the one holding it - and those categories are <see cref="Own"/>. 
/// Population is the exception: a figure for slaves or prisoners needs two Players, the one holding them and the one whose people they are, and the catalogue has
/// nowhere to put the second because it is global and knows nothing about games or players.
/// </para>
/// <para>
/// <see cref="PerOtherPlayer"/> is how that is resolved without making the catalogue per-game. A category marked with it is shown once under its own
/// name - "Enemies" - but every <b>group</b> beneath it is a template: <c>GetPlayerAssetsHandler</c> stamps each one out per other Player in the game,
/// labelled from that Player's PeopleName. The fan-out is a read-time projection, so nothing is written per game and a group list cannot go stale.
/// </para>
/// </remarks>
public enum AssetSubjectScope
{
    /// <summary>
    /// The category is shown once, counting what this Player holds. Every category in the catalogue but the population templates.
    /// </summary>
    Own = 1,

    /// <summary>
    /// The category is shown once, but every group beneath it is a template, projected once per other Player in the game at read time.
    /// </summary>
    PerOtherPlayer = 2
}
