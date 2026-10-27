// Mirrors the API's RulerGender enum, which serialises by name (JsonStringEnumConverter).

export type RulerGender = "Male" | "Female";

/** The choices offered in the setup dialog, in display order. */
export const RULER_GENDERS: readonly RulerGender[] = ["Male", "Female"];

/**
 * Ruler titles that read as male.
 *
 * Kept in step by hand with `GetMaleRulerTitles()` in GameSetupService.cs, which draws from the same list when it names
 * a seeded slot. A C# array cannot be imported here, and an endpoint to serve three static string lists would be a lot
 * of machinery for text that changes about once a year.
 */
export const MALE_RULER_TITLES: readonly string[] = [
  "King",
  "Emperor",
  "High King",
  "Prince",
  "Duke",
  "Grand Duke",
  "Archduke",
  "Count",
  "Earl",
  "Lord",
  "Baron",
  "Sultan",
  "Shah",
  "Khan",
  "Overlord",
  "Lord Protector",
  "Imperator",
  "System Lord",
];

/** Ruler titles that read as female. Kept in step with `GetFemaleRulerTitles()` — see {@link MALE_RULER_TITLES}. */
export const FEMALE_RULER_TITLES: readonly string[] = [
  "Queen",
  "Empress",
  "High Queen",
  "Princess",
  "Duchess",
  "Grand Duchess",
  "Archduchess",
  "Countess",
  "Lady",
  "Baroness",
  "Sultana",
  "Shahbanu",
  "Khatun",
  "Overlady",
  "Lady Protector",
  "Imperatrix",
  "System Lady",
];

/** Titles that read either way. Kept in step with `GetNonGenderSpecificRulerTitles()` — see {@link MALE_RULER_TITLES}. */
export const NON_GENDER_SPECIFIC_RULER_TITLES: readonly string[] = [
  "Sovereign",
  "High Sovereign",
  "Monarch",
  "Ruler",
  "Regent",
  "Pharaoh",
  "Chieftain",
  "High Chieftain",
  "Warlord",
  "Supreme Commander",
  "Grand Marshal",
  "First Citizen",
  "Archon",
  "High Executor",
  "Planetary Governor",
  "Oracle-Ruler",
  "High Arbiter",
  "Dominion Speaker",
  "Thronekeeper",
];

/**
 * The titles to suggest for a ruler of the given gender: the gendered set first, then the ones that suit either.
 *
 * A null gender is answered with the non-specific titles alone, rather than both gendered lists at once: the
 * suggestions are a shortcut, and a list that changes under the user once they pick a gender is worse than a short
 * one. That branch is there for the contract rather than for the UI — null is how the API reports a slot nobody has
 * defined, but the setup dialog opens its gender field on a value, so it never asks for it. The field is free text
 * regardless, so nothing here restricts what a player can call themselves.
 */
export function titlesFor(gender: RulerGender | null): string[] {
  if (gender === "Male") {
    return [...MALE_RULER_TITLES, ...NON_GENDER_SPECIFIC_RULER_TITLES];
  }

  if (gender === "Female") {
    return [...FEMALE_RULER_TITLES, ...NON_GENDER_SPECIFIC_RULER_TITLES];
  }

  return [...NON_GENDER_SPECIFIC_RULER_TITLES];
}
