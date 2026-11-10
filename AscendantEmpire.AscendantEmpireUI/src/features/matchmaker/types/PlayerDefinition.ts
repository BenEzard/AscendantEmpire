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

/**
 * A ruler portrait another player in the same game has already chosen. Shaped to match the image picker's `UsedImage`,
 * so the response goes straight into `usedValues` with no translation.
 */
export type UsedRulerPortrait = {
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
  // The people names. Non-nullable server-side: a seeded slot carries a placeholder, and a slot
  // from before the columns existed carries an empty string.
  peopleName: string;
  demonym: string;
  language: string;
  // Root-relative path to the chosen portrait, e.g. "/portraits/07_northern_queen.png". Non-nullable server-side: a seeded
  // slot already carries one, and a slot from before the column existed carries an empty string.
  rulerPortraitPath: string;
  // Never includes the caller's own colour, which must stay pickable.
  usedFactionColours: UsedFactionColour[];
  // Likewise never the caller's own portrait. Only portraits a rival has actually chosen appear; the default is the one
  // value a game may hold more than once, so an undecided rival takes nothing out of the set.
  usedRulerPortraits: UsedRulerPortrait[];
  // True once play has begun, when every field but the ruler title has become read-only for the life of the game. The
  // server enforces it either way; this is what lets the dialog disable those fields rather than fail on submit.
  isLocked: boolean;
};

/**
 * One field of a definition whose requested value another player in the game already holds.
 *
 * `field` is named as the PUT body spells it, so it keys straight into the draft without translation.
 */
export type PlayerDefinitionFieldConflict = {
  field: string;
  message: string;
};

/**
 * The `detail` a 409 from the definition save carries: every clashing field at once.
 *
 * Mirrors the API's `PlayerDefinitionConflict`. Reported together rather than one at a time, so a player who picks three
 * taken values is told all three instead of resubmitting three times.
 */
export type PlayerDefinitionConflict = {
  fields: PlayerDefinitionFieldConflict[];
};

/** The PUT body. Carries no identity: the game comes from the route and the slot from the token. */
export type UpdatePlayerDefinitionBody = {
  // Blank is allowed; the server stores its default placeholder title for it.
  rulerTitle: string;
  rulerName: string;
  rulerGender: RulerGender;
  factionName: string;
  factionColour: string;
  peopleName: string;
  demonym: string;
  language: string;
  rulerPortraitPath: string;
};
