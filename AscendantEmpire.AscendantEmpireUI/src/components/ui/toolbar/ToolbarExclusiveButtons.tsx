import { useRef, type KeyboardEvent, type ReactNode } from "react";
import DropButton, { type DropButtonItem } from "@/components/ui/DropButton";
import Tooltip from "@/components/ui/Tooltip";

// Self-import the toolbar styles so this component is usable on its own (e.g. inside dialogs that
// don't render a <Toolbar>). The .toolbar-exclusive / .toolbar-button rules live in Toolbar.css.
import "./Toolbar.css";

export type ExclusiveToolKey = string;

export type ToolbarExclusiveItem =
  | {
      type: "button";
      key: ExclusiveToolKey;
      icon: ReactNode;
      tooltip?: string;
      /** Names an icon-only button for assistive tech, which cannot read the tooltip. */
      ariaLabel?: string;
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
  // The group as a whole is a single tab stop (roving tabindex), so Tab lands on one button and the
  // arrow keys move between them — the standard radiogroup interaction. This holds the wrapper so the
  // keydown handler can find the radios in DOM order.
  const containerRef = useRef<HTMLSpanElement>(null);

  // Which item owns the single tab stop: the selected one, or the first item when nothing is selected.
  const rovingTabKey = value ?? items[0]?.key ?? null;

  // Arrow keys move focus to the next/previous selectable button and select it (select-on-arrow, the
  // standard radiogroup behaviour). We reuse each button's own click handler so arrowing is identical
  // to clicking — same onChange and onSelect side effects, no duplicated logic.
  const handleKeyDown = (event: KeyboardEvent<HTMLSpanElement>) => {
    if (disabled) {
      return;
    }
    const isForward = event.key === "ArrowRight" || event.key === "ArrowDown";
    const isBackward = event.key === "ArrowLeft" || event.key === "ArrowUp";
    if (!isForward && !isBackward) {
      return;
    }

    // Only plain buttons carry role="radio"; this naturally excludes dropbuttons from arrow navigation
    // (they keep their own keyboard behaviour and remain reachable by Tab).
    const radios = Array.from(containerRef.current?.querySelectorAll<HTMLButtonElement>('[role="radio"]') ?? []).filter(
      (radio) => !radio.disabled,
    );
    if (radios.length === 0) {
      return;
    }

    // If focus isn't currently on one of our radios (e.g. it's on a dropbutton), don't hijack the keys.
    const currentIndex = radios.findIndex((radio) => radio === document.activeElement);
    if (currentIndex === -1) {
      return;
    }

    event.preventDefault();
    const nextIndex = (currentIndex + (isForward ? 1 : -1) + radios.length) % radios.length;
    const nextRadio = radios[nextIndex];
    nextRadio.focus();
    nextRadio.click();
  };

  return (
    <span
      ref={containerRef}
      className="toolbar-exclusive"
      role="radiogroup"
      aria-disabled={disabled}
      onKeyDown={handleKeyDown}>
      {items.map((item) => {
        const effectiveDisabled = disabled;
        const active = !effectiveDisabled && value === item.key;

        if (item.type === "dropbutton") {
          return (
            <Tooltip key={item.key} content={item.tooltip}>
              <span className={"toolbar-btn-wrapper"}>
                <DropButton
                  label={item.label}
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
            </Tooltip>
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
          <Tooltip key={item.key} content={item.tooltip}>
            <span className={effectiveDisabled ? "toolbar-btn-wrapper toolbar-btn-wrapper--disabled" : "toolbar-btn-wrapper"}>
              <button
                type="button"
                className={className}
                disabled={effectiveDisabled}
                aria-label={item.ariaLabel}
                aria-pressed={active}
                role="radio"
                aria-checked={active}
                // Roving tabindex: only the tab-stop owner is reachable by Tab; the rest are reached
                // by arrowing within the group (see handleKeyDown).
                tabIndex={effectiveDisabled || item.key !== rovingTabKey ? -1 : 0}
                onClick={() => {
                  if (effectiveDisabled) return;
                  onChange(item.key);
                  item.onSelect?.();
                }}>
                {item.icon}
              </button>
            </span>
          </Tooltip>
        );
      })}
    </span>
  );
}
