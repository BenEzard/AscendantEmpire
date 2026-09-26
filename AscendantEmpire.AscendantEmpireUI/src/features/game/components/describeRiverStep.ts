import type { StepForceResponse } from "../api/forceMovementApi";
import { riverWidthLabel } from "@/types/map/Rivers";

/**
 * Says what the rivers did to a step the player just made, or null when no river was involved.
 *
 * The step's response carries the geometry (which river it crossed, which it ran along) and the price (what the step cost, and how
 * much of that the rivers accounted for). Both come from the server, because the movement rules live there and differ by movement
 * domain — a land force pays to cross a medium river and saves by following one, a ship sails up a large river for a flat rate, and
 * an air force crosses rivers without noticing them. So this only ever *reports* figures; it never works one out.
 *
 * Following a river covers a ship sailing up it as well: the response says the step ran along a large river and what it cost, which
 * is true of both, and it does not say which domain the force moves in.
 *
 * Pure, and separate from the toast that shows it, so the wording can be pinned without rendering the game.
 */
export function describeRiverStep(result: Pick<StepForceResponse, "riverCrossed" | "riverAlong" | "stepCost" | "riverMovementCost">): string | null {
  const crossed = riverWidthLabel(result.riverCrossed);
  const along = riverWidthLabel(result.riverAlong);

  if (crossed == null && along == null) {
    return null;
  }

  // One step can do both — crossing a tributary while following the river it feeds — and then both are named, in that order.
  const clauses: string[] = [];

  if (crossed != null) {
    clauses.push(`Crossed a ${crossed} river`);
  }

  if (along != null) {
    // Only the opening clause is capitalised, so "followed" reads as a continuation when it comes second.
    clauses.push(crossed == null ? `Followed a ${along} river` : `followed a ${along} river`);
  }

  return `${clauses.join(" and ")}. ${describeCost(result.stepCost, result.riverMovementCost)}`;
}

/**
 * The price of the step, and what the rivers added to or took off it.
 *
 * One net figure for the rivers, because a step that both crosses and follows one has a single adjustment and splitting it between
 * the two would be an invention. Where the rivers changed nothing — an air force, which ignores them — only the cost is given.
 */
function describeCost(stepCost: number, riverMovementCost: number): string {
  if (riverMovementCost > 0) {
    return `The step cost ${stepCost} MP, ${riverMovementCost} more than the ground alone.`;
  }

  if (riverMovementCost < 0) {
    return `The step cost ${stepCost} MP, ${-riverMovementCost} less than the ground alone.`;
  }

  return `The step cost ${stepCost} MP.`;
}
