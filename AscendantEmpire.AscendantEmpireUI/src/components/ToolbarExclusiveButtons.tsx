import type { ReactNode } from "react";
import DropButton, { type DropButtonItem } from "@/components/ui/DropButton";

export type ExclusiveToolKey = string;

export type ToolbarExclusiveItem =
  | {
      type: "button";
      key: ExclusiveToolKey;
      icon: ReactNode;
      tooltip?: string;
      onSelect?: () => void;
    }
  | {
      type: "dropbutton";
      key: ExclusiveToolKey;
      label: string;
      icon?: ReactNode;
      tooltip?: string;
      onClick: () => void;
      items: DropButtonItem[];
      menuDisabled?: boolean;
      menuLayout?: "list" | "grid";
      menuGridColumns?: number;
    };

export interface ToolbarExclusiveButtonsProps {
  items: ToolbarExclusiveItem[];
  value: ExclusiveToolKey | null;
  onChange: (next: ExclusiveToolKey) => void;
  disabled?: boolean;
  buttonVariant?: string;
}

export default function ToolbarExclusiveButtons({
  items,
  value,
  onChange,
  disabled = false,
  buttonVariant = "secondary",
}: ToolbarExclusiveButtonsProps) {
  return (
    <span className="toolbar-exclusive" role="radiogroup" aria-disabled={disabled}>
      {items.map((item) => {
        const effectiveDisabled = disabled;
        const active = !effectiveDisabled && value === item.key;

        if (item.type === "dropbutton") {
          return (
            <span key={item.key} title={item.tooltip} className={"toolbar-btn-wrapper"}>
              <DropButton
                label={item.label}
                title={item.tooltip}
                icon={item.icon}
                disabled={effectiveDisabled}
                onClick={() => {
                  if (effectiveDisabled) return;
                  onChange(item.key);
                  item.onClick();
                }}
                items={item.items}
                menuDisabled={item.menuDisabled}
                menuLayout={item.menuLayout}
                menuGridColumns={item.menuGridColumns}
                frameClassName={active ? "toolbar-button--active" : undefined}
              />
            </span>
          );
        }

        const className = [
          "toolbar-button",
          active ? "toolbar-button--active" : "",
          effectiveDisabled ? "toolbar-button--disabled" : "",
          buttonVariant ? `toolbar-button--${buttonVariant}` : "",
        ]
          .filter(Boolean)
          .join(" ");

        return (
          <span
            key={item.key}
            title={item.tooltip}
            className={effectiveDisabled ? "toolbar-btn-wrapper toolbar-btn-wrapper--disabled" : "toolbar-btn-wrapper"}>
            <button
              type="button"
              className={className}
              disabled={effectiveDisabled}
              aria-pressed={active}
              role="radio"
              aria-checked={active}
              onClick={() => {
                if (effectiveDisabled) return;
                onChange(item.key);
                item.onSelect?.();
              }}>
              {item.icon}
            </button>
          </span>
        );
      })}
    </span>
  );
}
