import { isValidElement } from "react";
import { getForceActionIcon, getForceActionLabel } from "../forceActionRegistry";
import type { ForceActionType } from "../../api/forceContextActionsApi";

/**
 * The registry is the one place the frontend decides how a backend-driven action looks. What matters is that every
 * action type the backend can send resolves to its own icon and this build's own wording, and that a type from a
 * newer backend still renders rather than crashing or vanishing.
 */
describe("forceActionRegistry", () => {
  const knownTypes: ForceActionType[] = ["SplitForce", "CreateVillage", "EstablishCamp", "Battle"];

  it.each(knownTypes)("gives %s its own icon", (actionType) => {
    expect(isValidElement(getForceActionIcon(actionType))).toBe(true);
  });

  /**
   * A shared icon would be a real bug rather than a cosmetic one: the toolbar draws icons only, so two actions
   * with the same glyph are indistinguishable to the player.
   */
  it("gives every known action type a distinct icon", () => {
    const iconTypes = knownTypes.map((actionType) => {
      const icon = getForceActionIcon(actionType);
      return isValidElement(icon) ? icon.type : icon;
    });

    expect(new Set(iconTypes).size).toBe(knownTypes.length);
  });

  it.each([
    ["SplitForce", "Split Force"],
    ["CreateVillage", "Create Village"],
    ["EstablishCamp", "Establish Camp"],
  ] as const)("prefers this build's own label for %s", (actionType, expected) => {
    expect(getForceActionLabel(actionType, "whatever the backend said")).toBe(expected);
  });

  /**
   * The backend can serve an action type from a newer build than the frontend. That has to degrade to a generic
   * button carrying the backend's own label, not to a blank space or a thrown error.
   */
  it("falls back to a generic icon and the backend's label for an unrecognised action type", () => {
    const unknownType = "FoundEmpire" as ForceActionType;

    expect(isValidElement(getForceActionIcon(unknownType))).toBe(true);
    expect(getForceActionLabel(unknownType, "Found Empire")).toBe("Found Empire");
  });
});
