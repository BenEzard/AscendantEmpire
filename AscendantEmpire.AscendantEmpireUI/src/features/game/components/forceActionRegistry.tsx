import type { ReactNode } from "react";
import { FaCircleQuestion } from "react-icons/fa6";
import { GiSplitArrows } from "react-icons/gi";
import type { ForceActionType } from "../api/forceContextActionsApi";

/**
 * How one backend-driven action is presented.
 *
 * Icon and label live here rather than coming down from the API deliberately: both are presentation concerns,
 * and the backend would otherwise have to know about the frontend's icon set to add an action. The backend
 * still sends a label, which is used as the fallback below for an action type this registry hasn't learned yet.
 */
interface TroopActionPresentation {
  icon: ReactNode;
  label: string;
}

/**
 * Presentation for each action type the frontend knows about.
 *
 * Partial on purpose — the backend may serve an action type from a newer build than the frontend, and that must
 * degrade to a generic button rather than crashing or vanishing.
 */
const TROOP_ACTION_PRESENTATION: Partial<Record<ForceActionType, TroopActionPresentation>> = {
  SplitForce: { icon: <GiSplitArrows />, label: "Split Force" },

  // TODO: Hunt used to live in the toolbar, gated on legacy unit design id 2, which no longer exists now that
  // troops reference unit templates (whose ids are database-dependent, so a magic number can't stand in). It now
  // belongs behind a compiled capability tag (e.g. "hunting") as its own backend strategy — see
  // docs/design/unit-design/tags-and-modifiers.md, concept 3.
};

/** Shown for an action type this build doesn't recognise, alongside the backend's own label. */
const UNKNOWN_ACTION_ICON = <FaCircleQuestion />;

/**
 * Display-order slots for actions that exist only on the frontend and have no backend rule behind them.
 *
 * These come from the band the C# ForceActionType enum reserves (1–999) and which the backend never emits, so
 * frontend-only affordances sort into the same sequence as the backend-driven ones without ever colliding.
 */
export const FRONTEND_ONLY_ACTION_ORDER = {
  /** Reopens the multi-troop selection dialog. Sorts ahead of everything the backend sends. */
  selectTroop: 100,
} as const;

/** The icon for an action type, falling back to a generic one when the type isn't recognised. */
export function getForceActionIcon(actionType: ForceActionType): ReactNode {
  return TROOP_ACTION_PRESENTATION[actionType]?.icon ?? UNKNOWN_ACTION_ICON;
}

/**
 * The label for an action type, preferring this build's own wording and falling back to whatever the backend
 * sent for an action type it doesn't know.
 */
export function getForceActionLabel(actionType: ForceActionType, backendLabel: string): string {
  return TROOP_ACTION_PRESENTATION[actionType]?.label ?? backendLabel;
}
