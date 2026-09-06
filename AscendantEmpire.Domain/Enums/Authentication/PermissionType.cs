namespace AscendantEmpire.Domain.Enums.Authentication;

/// <summary>
/// A capability that can be granted to a <see cref="Entities.Identity.User"/>, gating either a player action or access to one of the developer tools.
/// </summary>
/// <remarks>
/// <para>
/// The set is fixed by the code paths it guards, so it lives here rather than in a database catalogue. Each member has a matching authorization policy
/// registered by name at startup, applied to controllers as <c>[Authorize(Policy = nameof(PermissionType.X))]</c>.
/// </para>
/// <para>
/// Values are explicit because they are persisted as integers in <c>auth.UserPermission</c>; never renumber an existing member. The names travel on the
/// wire (the API serialises enums by name) and appear as repeated <c>permission</c> claims in the access token.
/// </para>
/// </remarks>
public enum PermissionType
{
    /// <summary>Set up a new game.</summary>
    CreateGame = 1,

    /// <summary>Claim a player slot in an existing game.</summary>
    JoinGame = 2,

    /// <summary>Use the System panel, including listing and deactivating accounts and granting these permissions.</summary>
    SystemPanel = 3,

    /// <summary>Use the Entity Setup Tool, including seeding the database and editing the shared component, tag and template catalogues.</summary>
    EntitySetupTool = 4,

    /// <summary>Use the Map Editor, including creating, editing and publishing campaign maps and editing the biome and geography catalogues.</summary>
    MapEditor = 5,

    /// <summary>Permanently delete a Game and everything scoped to it, from the System panel's Games page.</summary>
    /// <remarks>
    /// Deliberately not part of the default grant a new account receives, and deliberately separate from <see cref="SystemPanel"/>: reaching the System
    /// panel is a much smaller thing to hand out than being able to destroy a game other people are playing.
    /// </remarks>
    DeleteGame = 6,
}
