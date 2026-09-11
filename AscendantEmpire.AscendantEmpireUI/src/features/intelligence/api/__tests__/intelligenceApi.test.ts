import { normalizeFidelity, normalizeKnownForce, normalizeSightingReport } from "../intelligenceApi";

/**
 * The wire normalisers sit between the redaction layer on the server and everything the player sees, so their one job beyond filling defaults is never to
 * invent what the server withheld.
 */
describe("intelligenceApi normalisers", () => {
  /**
   * Fails closed, the same way the server does: a fidelity the client does not recognise must never be read as licence to show more.
   */
  it("reads an unrecognised or missing fidelity as the lowest level", () => {
    expect(normalizeFidelity("Owner")).toBe("Owner");
    expect(normalizeFidelity("Omniscient")).toBe("Movement");
    expect(normalizeFidelity(undefined)).toBe("Movement");
  });

  /**
   * A withheld subject field arrives absent or null, and has to stay null. Any default — a 0 template, a 0 owner — would be exactly the disclosure the
   * server refused to make.
   */
  it("keeps withheld subject fields null rather than defaulting them", () => {
    const report = normalizeSightingReport({ reportId: 7, fidelity: "Movement", path: [{ x: 4, y: 1 }] });

    expect(report).not.toBeNull();
    expect(report!.unitTemplateId).toBeNull();
    expect(report!.ownerPlayerId).toBeNull();
    expect(report!.unitInstanceId).toBeNull();
    expect(report!.healthyTroops).toBeNull();
  });

  /**
   * A report is a statement about where something was. One with no usable tile cannot be shown, and pinning it to (0, 0) would send the player to a corner
   * of the map for nothing.
   */
  it("drops a report with no usable path, and any half-coordinate within one", () => {
    expect(normalizeSightingReport({ reportId: 7, path: [] })).toBeNull();
    expect(normalizeSightingReport({ reportId: 7, path: [{ x: 3 }] })).toBeNull();

    const report = normalizeSightingReport({ reportId: 8, path: [{ x: 3 }, { x: 4, y: 1 }] });

    expect(report!.path).toEqual([{ x: 4, y: 1 }]);
  });

  it("drops a known force with nowhere to draw it", () => {
    expect(normalizeKnownForce({ status: "Sighted", lastSeenLocation: null })).toBeNull();
    expect(normalizeKnownForce({ status: "Destroyed", lastSeenLocation: { x: 2, y: 5 } })?.lastSeenLocation).toEqual({ x: 2, y: 5 });
  });
});
