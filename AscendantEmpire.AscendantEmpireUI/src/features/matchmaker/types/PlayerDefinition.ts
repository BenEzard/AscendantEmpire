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
  // The four people-and-culture names. Non-nullable server-side: a seeded slot carries a placeholder, and a slot
  // from before the columns existed carries an empty string.
  peopleName: string;
  demonym: string;
  cultureName: string;
  language: string;
  // Root-relative path to the chosen portrait, e.g. "/portraits/ruler_07.png". Non-nullable server-side: a seeded
  // slot already carries one, and a slot from before the column existed carries an empty string.
  rulerPortraitPath: string;
  // Never includes the caller's own colour, which must stay pickable.
  usedFactionColours: UsedFactionColour[];
  // Likewise never the caller's own portrait. Only portraits a rival has actually chosen appear; the default is the one
  // value a game may hold more than once, so an undecided rival takes nothing out of the set.
  usedRulerPortraits: UsedRulerPortrait[];
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
  cultureName: string;
  language: string;
  rulerPortraitPath: string;
};
