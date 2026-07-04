// Client-side slot acceptance-rule evaluator. Mirrors the backend SlotRuleEvaluator so the designer
// only offers a component for a slot when it would actually be accepted server-side.
//
// A candidate must satisfy ALL of a slot's rules. Each rule matches on one attribute of the candidate:
//   - "ComponentType": the candidate's componentType.
//   - "HasTag": whether the candidate carries a given tag code.
// The operator controls the comparison; `In` treats the rule value as a comma-separated list.

import type { UnitComponent, UnitComponentSlot, UnitComponentSlotRule } from "../types/UnitComponent";

/** Splits a rule value into trimmed, non-empty tokens (for the `In` operator's comma-separated list). */
function ruleValues(rule: UnitComponentSlotRule): string[] {
  return rule.value
    .split(",")
    .map((token) => token.trim())
    .filter((token) => token.length > 0);
}

/** True when a single rule is satisfied by the candidate component. */
function ruleMatches(component: UnitComponent, rule: UnitComponentSlotRule): boolean {
  const trimmed = rule.value.trim();

  if (rule.ruleType === "ComponentType") {
    switch (rule.operator) {
      case "Equals":
        return component.componentType === trimmed;
      case "NotEquals":
        return component.componentType !== trimmed;
      case "In":
        return ruleValues(rule).includes(component.componentType);
      default:
        return false;
    }
  }

  // "HasTag": compare against the candidate's attached tag codes.
  switch (rule.operator) {
    case "Equals":
      return component.tagCodes.includes(trimmed);
    case "NotEquals":
      return !component.tagCodes.includes(trimmed);
    case "In":
      return ruleValues(rule).some((tagCode) => component.tagCodes.includes(tagCode));
    default:
      return false;
  }
}

/**
 * True when a candidate component may be fitted into the given slot. A Body is a template's root and is
 * never fitted into another component's slot, so it is always rejected. Otherwise every rule must pass
 * (a slot with no rules accepts any non-Body component).
 */
export function componentSatisfiesSlot(component: UnitComponent, slot: UnitComponentSlot): boolean {
  if (component.componentType === "Body") {
    return false;
  }

  return slot.rules.every((rule) => ruleMatches(component, rule));
}
