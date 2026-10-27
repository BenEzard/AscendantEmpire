// The ruler portraits the setup dialog offers. The files ship in the UI's public/portraits folder, so the paths here are
// root-relative client URLs - the same arrangement as Asset.IconPath - and the API never resolves one.
//
// public/ rather than src/assets, where most of the UI's images sit, because a chosen path is stored on the Player row
// and comes back through the API: a bundled asset is served under a build-time content hash, so every stored row would
// break on the next build that touched its file. It is also why renaming one of these files is a data change.

import type { RulerGender } from "./RulerGender";

/** One portrait as the picker offers it. */
export type RulerPortrait = {
  /**
   * The value stored on the Player row, e.g. "/portraits/01_western_king.png".
   *
   * Root-relative rather than relative, because the UI is a multi-page app: a relative path would resolve differently
   * from /game/ than from /matchmaker/, and both pages render a portrait.
   */
  path: string;

  /** The name the tile is announced and labelled by, e.g. "Western King". */
  label: string;

  /** The gender the face reads as, which is what the picker filters on. Every portrait reads as one or the other. */
  gender: RulerGender;
};

/** The folder every portrait file sits in, as a root-relative client URL. */
const PORTRAIT_FOLDER_URL = "/portraits";

/** Builds one catalogue entry, so the table below reads as the three facts about a portrait rather than as path strings. */
function portrait(fileName: string, label: string, gender: RulerGender): RulerPortrait {
  return { path: `${PORTRAIT_FOLDER_URL}/${fileName}`, label, gender };
}

/**
 * Every portrait that ships in public/portraits, in file order.
 *
 * Listed by hand rather than generated from a count, and rather than globbed: Vite cannot enumerate public/ at build
 * time - contrast unitDisplayImageResolver.ts, which globs src/assets/units precisely because those files *are*
 * bundled - and a count could not carry the gender or the label anyway. The file name is part of the stored value, so
 * renaming a file here renames it on every Player row that holds it.
 */
export const RULER_PORTRAITS: readonly RulerPortrait[] = [
  portrait("01_western_king.png", "Western King", "Male"),
  portrait("02_western_queen.png", "Western Queen", "Female"),
  portrait("03_east_asian_ruler.png", "East Asian Ruler", "Male"),
  portrait("04_egyptian_queen.png", "Egyptian Queen", "Female"),
  portrait("05_northern_king.png", "Northern King", "Male"),
  portrait("06_african_king.png", "African King", "Male"),
  portrait("07_northern_queen.png", "Northern Queen", "Female"),
  portrait("08_turbaned_ruler.png", "Turbaned Ruler", "Male"),
  portrait("09_east_asian_queen.png", "East Asian Queen", "Female"),
  portrait("10_roman_ruler.png", "Roman Ruler", "Male"),
  portrait("11_tribal_queen.png", "Tribal Queen", "Female"),
  portrait("12_viking_king.png", "Viking King", "Male"),
  portrait("13_african_queen.png", "African Queen", "Female"),
  portrait("14_desert_ruler.png", "Desert Ruler", "Male"),
  portrait("15_steppe_ruler.png", "Steppe Ruler", "Male"),
  portrait("16_imperial_ruler.png", "Imperial Ruler", "Male"),
  portrait("17_blonde_queen.png", "Blonde Queen", "Female"),
  portrait("18_red_headband_ruler.png", "Red Headband Ruler", "Male"),
  portrait("19_red_hat_ruler.png", "Red Hat Ruler", "Male"),
  portrait("20_blue_crowned_queen.png", "Blue Crowned Queen", "Female"),
];

/**
 * The portraits offered for a ruler of the given gender.
 *
 * The picker only ever shows one gender's worth, because the dialog's gender field always holds a value - it opens on
 * Male for a slot that has never been defined - so there is no "show everything" state to fall back to.
 */
export function rulerPortraitsFor(gender: RulerGender): readonly RulerPortrait[] {
  return RULER_PORTRAITS.filter((candidate) => candidate.gender === gender);
}

/**
 * True for a path the picker still offers for the given gender.
 *
 * Asks "is the portrait this slot holds still a legitimate choice?" of a definition read back from the API, which a
 * stored path can fail on either of two counts: the file has since been removed or renamed, or it belongs to the
 * gender the slot does not hold. Both have to read as no portrait rather than be saved straight back.
 */
export function isRulerPortraitOffered(path: string, gender: RulerGender): boolean {
  return RULER_PORTRAITS.some((candidate) => candidate.path === path && candidate.gender === gender);
}
