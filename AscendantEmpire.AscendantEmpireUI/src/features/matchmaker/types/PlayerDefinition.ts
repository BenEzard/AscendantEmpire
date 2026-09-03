// Mirrors the API's IPlayerDefinition / IUsedFactionColour (camelCased by ASP.NET's JSON serializer).

import type { RulerGender } from "./RulerGender";

/**
 * A faction colour another player in the same game already holds. Shaped to match the colour picker's `UsedColor`, so
 * the response goes straight into `usedColors` with no translation.
 */
export type UsedFactionColour = {
  value: string;
  usedBy: string;
};

/** The caller's own slot in one game, plus the colours their rivals hold. */
export type PlayerDefinition = {
  playerId: number;
  gameId: number;
  rulerTitle: string;
  rulerName: string;
  // Null until the player has defined themselves; a seeded slot asserts no gender.
  rulerGender: RulerGender | null;
  factionName: string;
  // Null until the player has picked one.
  factionColour: string | null;
  // Never includes the caller's own colour, which must stay pickable.
  usedFactionColours: UsedFactionColour[];
};

/** The PUT body. Carries no identity: the game comes from the route and the slot from the token. */
export type UpdatePlayerDefinitionBody = {
  // Blank is allowed; the server stores its default placeholder title for it.
  rulerTitle: string;
  rulerName: string;
  rulerGender: RulerGender;
  factionName: string;
  factionColour: string;
};
