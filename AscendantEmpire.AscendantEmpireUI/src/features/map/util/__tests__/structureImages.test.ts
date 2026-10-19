import { structureImageSrc } from "../structureImages";

/**
 * The art table is keyed on the catalogue code, not the structure id, and this is the guard on that.
 *
 * It used to be keyed on the id, which only ever worked because the single catalogue row anyone had was inserted by hand. A
 * catalogue id is a database identity, so the seed would have had to hand out the same numbers on every database for the art to
 * line up — and would have silently drawn the wrong sprite the first time it did not.
 */
describe("structureImageSrc", () => {
  it.each(["camp", "village", "wooden-watch-tower", "palisade", "wooden-totem-pole"])(
    "has art for the seeded kind %s",
    (code) => {
      expect(structureImageSrc(code)).toBeTruthy();
    },
  );

  /** Each sprite says which kind it is, so two kinds sharing one would make them indistinguishable on the map. */
  it("gives each kind its own distinct sprite", () => {
    const codes = ["camp", "village", "wooden-watch-tower", "palisade", "wooden-totem-pole"];
    const sources = codes.map((code) => structureImageSrc(code));

    expect(new Set(sources).size).toBe(codes.length);
  });

  /** A catalogue the client is older than can name kinds it has no sprite for, and the caller draws nothing rather than guessing. */
  it("has nothing for a kind it does not know", () => {
    expect(structureImageSrc("aqueduct")).toBeUndefined();
  });

  /**
   * An API too old to send a code normalizes to an empty string, and a kind can legitimately be absent. Neither is a lookup
   * worth making, so both answer undefined rather than reaching the map at all.
   */
  it.each([["", "an empty code"], [null, "null"], [undefined, "undefined"]])(
    "has nothing for %s (%s)",
    (code) => {
      expect(structureImageSrc(code as string | null | undefined)).toBeUndefined();
    },
  );
});
