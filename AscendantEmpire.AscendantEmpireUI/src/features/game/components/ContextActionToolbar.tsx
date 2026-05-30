import type { ReactNode } from "react";
import Tooltip from "@/components/ui/Tooltip";

export interface ContextActionToolbarAction {
  key: string;
  label: string;
  icon: ReactNode;
  onClick: () => void;
}

export interface ContextActionToolbarProps {
  visible: boolean;
  actions: ContextActionToolbarAction[];
}

export default function ContextActionToolbar({ visible, actions }: ContextActionToolbarProps) {
  if (!visible || actions.length === 0) return null;

  return (
    <div className="command-toolbox" aria-label="Context action toolbar">
      {actions.map((action) => (
        <Tooltip key={action.key} content={action.label}>
          <button type="button" className="command-toolbox__action" onClick={action.onClick}>
            <span className="command-toolbox__icon" aria-hidden>
              {action.icon}
            </span>
            <span className="command-toolbox__label">{action.label}</span>
          </button>
        </Tooltip>
      ))}
    </div>
  );
}
