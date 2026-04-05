import type { ReactNode } from "react";

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
        <button key={action.key} type="button" className="command-toolbox__action" onClick={action.onClick} title={action.label}>
          <span className="command-toolbox__icon" aria-hidden>
            {action.icon}
          </span>
          <span className="command-toolbox__label">{action.label}</span>
        </button>
      ))}
    </div>
  );
}
