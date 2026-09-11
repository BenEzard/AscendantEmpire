import type { KnownForce } from "../api/intelligenceApi";
import {
  KNOWN_FORCE_FADE_ROUNDS,
  KNOWN_FORCE_FRESH_OPACITY,
  KNOWN_FORCE_STALE_OPACITY,
  groupKnownForcesByUnobservedTile,
  headingDegrees,
  knownForceOpacity,
} from "../knownForceMarkers";

function knownForce(x: number, y: number, lastSeenPlayerTurnId: number, lastSeenTurnSequence = 1): KnownForce {
  return {
    status: "Sighted",
    fidelity: "Detail",
    lastSeenLocation: { x, y },
    lastSeenPlayerTurnId,
    lastSeenRoundNumber: 1,
    lastSeenTurnSequence,
    exitDirection: null,
    stillVisible: false,
    unitTemplateId: 3,
    ownerPlayerId: 1,
    unitInstanceId: 99,
    healthyTroops: 45,
  };
}

describe("knownForceOpacity", () => {
  it("draws a force seen this round at full marker strength", () => {
    expect(knownForceOpacity(4, 4)).toBe(KNOWN_FORCE_FRESH_OPACITY);
  });

  it("fades linearly with the rounds since the force was seen", () => {
    const halfway = knownForceOpacity(1, 1 + KNOWN_FORCE_FADE_ROUNDS / 2);

    expect(halfway).toBeCloseTo((KNOWN_FORCE_FRESH_OPACITY + KNOWN_FORCE_STALE_OPACITY) / 2);
  });

  /**
   * Whether markers should ever be retired is an open design question, so age alone never hides one: it fades to a floor and stays there.
   */
  it("stops fading at the stale floor rather than disappearing", () => {
    expect(knownForceOpacity(1, 1 + KNOWN_FORCE_FADE_ROUNDS * 10)).toBe(KNOWN_FORCE_STALE_OPACITY);
  });

  /**
   * Showing a memory too clearly is harmless; fading one on no evidence is not. An unknown round and a round from a stale cache both draw fresh.
   */
  it("draws fresh when the current round is unknown or earlier than the sighting", () => {
    expect(knownForceOpacity(3, null)).toBe(KNOWN_FORCE_FRESH_OPACITY);
    expect(knownForceOpacity(5, 2)).toBe(KNOWN_FORCE_FRESH_OPACITY);
  });
});

describe("headingDegrees", () => {
  /** Screen rotation is clockwise and map Y grows south, so an east-pointing arrow turns a quarter clockwise to point south. */
  it("rotates an east-pointing arrow clockwise, with south a quarter turn", () => {
    expect(headingDegrees("East")).toBe(0);
    expect(headingDegrees("South")).toBe(90);
    expect(headingDegrees("North")).toBe(270);
    expect(headingDegrees("NorthEast")).toBe(315);
  });
});

describe("groupKnownForcesByUnobservedTile", () => {
  /**
   * Under live sight the map already shows what is really on a tile, so a memory there would at best repeat it and at worst draw a force the player can see is gone.
   */
  it("leaves out every tile currently in sight", () => {
    const grouped = groupKnownForcesByUnobservedTile([knownForce(2, 3, 10), knownForce(5, 5, 10)], new Set(["2,3"]));

    expect([...grouped.keys()]).toEqual(["5,5"]);
  });

  it("puts the freshest sighting on a tile first, since that is the one the marker shows", () => {
    const older = knownForce(5, 5, 10, 8);
    const newerTurn = knownForce(5, 5, 12, 1);
    const laterInSameTurn = knownForce(5, 5, 10, 9);

    const grouped = groupKnownForcesByUnobservedTile([older, newerTurn, laterInSameTurn], new Set());

    expect(grouped.get("5,5")).toEqual([newerTurn, laterInSameTurn, older]);
  });
});
