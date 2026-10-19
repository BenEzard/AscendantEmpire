import type { ReactNode } from "react";
import { FaCircleQuestion } from "react-icons/fa6";
import { GiCampingTent, GiCrossedSwords, GiSplitArrows, GiVillage } from "react-icons/gi";
import type { ForceActionType } from "../api/forceContextActionsApi";

/**
 * How one backend-driven action is presented.
 *
 * Icon and label live here rather than coming down from the API deliberately: both are presentation concerns,
 * and the backend would otherwise have to know about the frontend's icon set to add an action. The backend
 * still sends a label, which is used as the fallback below for an action type this registry hasn't learned yet.
 */
interface ForceActionPresentation {
  icon: ReactNode;
  label: string;
}

/**
 * Presentation for each action type the frontend knows about.
 *
 * Partial on purpose — the backend may serve an action type from a newer build than the frontend, and that must
 * degrade to a generic button rather than crashing or vanishing.
 */
const FORCE_ACTION_PRESENTATION: Partial<Record<ForceActionType, ForceActionPresentation>> = {
  SplitForce: { icon: <GiSplitArrows />, label: "Split Force" },

  // Both are offered only to a force whose compiled template grants the matching capability (create-settlement /
  // create-early-camp, carried by the camping-materials component), so they simply won't be in the list for anything
  // else. Nothing here needs to know that — the backend has already decided.
  CreateVillage: { icon: <GiVillage />, label: "Create Village" },
  EstablishCamp: { icon: <GiCampingTent />, label: "Establish Camp" },

  // Offered only to a force that is fighting in a battle, and opens that battle's dialog.
  Battle: { icon: <GiCrossedSwords />, label: "Battle" },

  // TODO: Hunt used to live in the toolbar, gated on legacy unit design id 2, which no longer exists now that
  // forces reference unit templates (whose ids are database-dependent, so a magic number can't stand in). It now
  // belongs behind a compiled capability tag (e.g. "hunting") as its own backend strategy — see
  // docs/design/unit-design/tags-and-modifiers.md, concept 3.
};

/** Shown for an action type this build doesn't recognise, alongside the backend's own label. */
const UNKNOWN_ACTION_ICON = <FaCircleQuestion />;

// The C# ForceActionType enum reserves a 1–999 band the backend never emits, for actions that exist only on the
// frontend. There are none right now — "Select Force" was the last, and TM-5 replaced it with the always-visible
// force selection bar — so there is no order table here. The enum keeps its reservation for the next one.

/** The icon for an action type, falling back to a generic one when the type isn't recognised. */
export function getForceActionIcon(actionType: ForceActionType): ReactNode {
  return FORCE_ACTION_PRESENTATION[actionType]?.icon ?? UNKNOWN_ACTION_ICON;
}

/**
 * The label for an action type, preferring this build's own wording and falling back to whatever the backend
 * sent for an action type it doesn't know.
 */
export function getForceActionLabel(actionType: ForceActionType, backendLabel: string): string {
  return FORCE_ACTION_PRESENTATION[actionType]?.label ?? backendLabel;
}
