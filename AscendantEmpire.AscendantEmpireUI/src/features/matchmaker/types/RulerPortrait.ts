// The ruler portraits the setup dialog offers. The files ship in the UI's public/portraits folder, so the paths here are
// root-relative client URLs - the same arrangement as Asset.IconPath - and the API never resolves one.

/**
 * How many portraits ship in public/portraits.
 *
 * Kept in step by hand with `Player.RULER_PORTRAIT_COUNT`, which game setup uses when it seeds a slot's portrait. A C#
 * constant cannot be imported here, and an endpoint to serve twenty generated strings would be a lot of machinery for a
 * list that only changes when somebody adds a file.
 */
export const RULER_PORTRAIT_COUNT = 20;

/** One portrait as the picker offers it. */
export type RulerPortrait = {
  /**
   * The value stored on the Player row, e.g. "/portraits/ruler_01.png".
   *
   * Root-relative rather than relative, because the UI is a multi-page app: a relative path would resolve differently
   * from /game/ than from /matchmaker/, and both pages render a portrait.
   */
  path: string;

  /** The name the tile is announced and labelled by, e.g. "Portrait 1". */
  label: string;
};

/** The path for one portrait number, 1-based, zero-padded to two digits as the files are named. */
export function rulerPortraitPath(portraitNumber: number): string {
  return `/portraits/ruler_${String(portraitNumber).padStart(2, "0")}.png`;
}

/**
 * Every portrait, in file order.
 *
 * Generated from the count rather than discovered, because Vite cannot enumerate public/ at build time - contrast
 * unitDisplayImageResolver.ts, which globs src/assets/units precisely because those files *are* bundled.
 */
export const RULER_PORTRAITS: readonly RulerPortrait[] = Array.from({ length: RULER_PORTRAIT_COUNT }, (_, index) => ({
  path: rulerPortraitPath(index + 1),
  label: `Portrait ${index + 1}`,
}));
