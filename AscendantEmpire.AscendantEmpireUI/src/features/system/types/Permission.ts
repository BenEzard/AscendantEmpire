// Client-side model for the capabilities that can be granted to an account.
//
// Mirrors the backend PermissionType domain enum (AscendantEmpire.Domain.Enums.Authentication.PermissionType).
//
// The API registers a JsonStringEnumConverter (AscendantEmpire.Api/Program.cs), so enums travel as their NAMES,
// not their numeric values. Names match the C# members exactly so values round-trip without translation — the
// same convention as TagType in features/unitdesign/types/Tag.ts.

/** A capability that can be granted to a user. */
export type PermissionType = "CreateGame" | "JoinGame" | "SystemPanel" | "EntitySetupTool" | "MapEditor" | "DeleteGame";

/** Every permission, in the order the permissions dialog lists them: player capabilities first, then the tools. */
export const PERMISSIONS: readonly PermissionType[] = ["CreateGame", "JoinGame", "SystemPanel", "EntitySetupTool", "MapEditor", "DeleteGame"];

/** Human-readable label for each permission, used by the permissions dialog. */
export const PERMISSION_LABELS: Record<PermissionType, string> = {
  CreateGame: "Create a game",
  JoinGame: "Join a game",
  SystemPanel: "System panel",
  EntitySetupTool: "Entity Setup Tool",
  MapEditor: "Map Editor",
  DeleteGame: "Delete a game",
};

/** One-line explanation of what each permission allows, shown beside its checkbox. */
export const PERMISSION_DESCRIPTIONS: Record<PermissionType, string> = {
  CreateGame: "Set up a new game.",
  JoinGame: "Claim a player slot in an existing game.",
  SystemPanel: "Administer accounts and their permissions, and transfer catalogues.",
  EntitySetupTool: "Edit the shared component, tag and template catalogues, and seed the database.",
  MapEditor: "Create, edit and publish campaign maps, and edit the biome and geography catalogues.",
  DeleteGame: "Permanently delete a game and everything in it. There is no undo.",
};
