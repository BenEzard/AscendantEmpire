import type { ReactNode } from "react";
import Tooltip from "@/components/ui/Tooltip";

export interface ContextActionToolbarAction {
  key: string;
  /** The action's name. Not drawn on the button — it reaches the player through the tooltip and the aria-label. */
  label: string;
  icon: ReactNode;
  /** Where this action sits in the toolbar. The toolbar renders in ascending order. */
  displayOrder: number;
  /** Whether the action can be taken right now. Disabled actions are still rendered, greyed out. */
  isEnabled: boolean;
  /**
   * Why the action can't be taken, shown in place of the label in the tooltip. Empty when enabled.
   *
   * An action the force could never take is not sent here at all — it simply isn't in the list — so anything
   * disabled is something the player could plausibly make available, and telling them how is the point.
   */
  disabledReasons: string[];
  onClick: () => void;
}

export interface ContextActionToolbarProps {
  visible: boolean;
  actions: ContextActionToolbarAction[];
}

/** Joins the reasons into the one string the tooltip takes, keeping each on its own sentence. */
function buildTooltip(action: ContextActionToolbarAction): string {
  if (action.isEnabled || action.disabledReasons.length === 0) {
    return action.label;
  }

  return `${action.label} — ${action.disabledReasons.join(" ")}`;
}

export default function ContextActionToolbar({ visible, actions }: ContextActionToolbarProps) {
  if (!visible || actions.length === 0) return null;

  // Sorted here rather than trusted from the caller so the buttons hold their positions even when the actions
  // arrive from several sources (the backend's list plus the frontend-only affordances) or out of order.
  const orderedActions = [...actions].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className="command-toolbox" aria-label="Context action toolbar">
      {orderedActions.map((action) => (
        // The wrapper carries the not-allowed cursor, because the disabled button itself is made transparent to
        // hit-testing so that this span still receives the hover that shows the tooltip.
        <Tooltip
          key={action.key}
          content={buildTooltip(action)}
          className={action.isEnabled ? undefined : "command-toolbox__action-wrapper--disabled"}>
          <button
            type="button"
            className="command-toolbox__action"
            onClick={action.onClick}
            disabled={!action.isEnabled}
            // The button is disabled, so it isn't focusable and can't announce its own reason. Naming it with the
            // full tooltip text keeps the explanation available to a screen reader too, not just on hover.
            aria-label={buildTooltip(action)}
          >
            {/* Icon only. The label isn't rendered — it would double the toolbar's height for a caption the tooltip
                and the aria-label already carry, and vertical pixels here come straight off the map. */}
            <span className="command-toolbox__icon" aria-hidden>
              {action.icon}
            </span>
          </button>
        </Tooltip>
      ))}
    </div>
  );
}
