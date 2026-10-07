import React from "react";
import "./Toolbar.css";
import Tooltip from "@/components/ui/Tooltip";

export interface ToolBarButton {
  key: string;
  icon?: React.ReactNode;
  render?: (ctx: { disabled: boolean; active: boolean; tooltip?: string }) => React.ReactNode;
  tooltip?: string;
  disabled?: boolean;
  active?: boolean;
  domId?: string;
  onClick?: () => void;
  onMiddleClick?: () => void;
  middleClickHint?: string;
  onRightClick?: () => void;
}

export interface ToolBarGroup {
  caption: string;
  buttons: ToolBarButton[];
}

interface ToolBarProps {
  groups: ToolBarGroup[];

  /**
   * Optional content pinned to the toolbar's right-hand end.
   *
   * Rendered after the groups in a region that takes the leftover width, so the groups on the left keep their exact
   * positions - nothing shifts when the trailing content appears, changes or is left out. Meant for status the toolbar
   * reports rather than actions it offers; anything clickable belongs in a group.
   */
  trailing?: React.ReactNode;
  containerClassName?: string;
  showCaptions?: boolean;
  buttonVariant?: string;
}

function ToolBar({
  groups,
  trailing,
  containerClassName = "toolbar flex justify-start border-b border-slate-200 bg-stone-100",
  showCaptions = true,
  buttonVariant = "secondary",
}: ToolBarProps) {
  const handleMiddleClick = (btn: ToolBarButton) => (e: React.MouseEvent) => {
    // Middle click is button === 1
    if (e.button !== 1) return;
    if (btn.disabled) return;
    if (!btn.onMiddleClick) return;

    // Stop browser autoscroll / other default behaviors
    e.preventDefault();
    e.stopPropagation();

    btn.onMiddleClick();
  };

  const handleRightClick = (btn: ToolBarButton) => (e: React.MouseEvent) => {
    if (!btn.onRightClick) return;

    // Stop the browser context menu.
    e.preventDefault();
    e.stopPropagation();

    btn.onRightClick();
  };

  return (
    <div className={containerClassName}>
      {groups.map((group) => (
        <div key={group.caption} className="toolbar-group">
          <div className="toolbar-button-group" aria-label={group.caption}>
            {group.buttons.map((btn) => {
              const middleHint = btn.middleClickHint ?? "add new";
              const tooltip = btn.onMiddleClick && btn.tooltip ? `${btn.tooltip} (middle-click: ${middleHint})` : btn.tooltip;
              const disabled = !!btn.disabled;
              const active = !!btn.active;

              if (btn.render) {
                return (
                  <Tooltip key={btn.key} content={tooltip}>
                    <span className={"toolbar-btn-wrapper"} onContextMenu={handleRightClick(btn)}>
                      {btn.render({ disabled, active, tooltip })}
                    </span>
                  </Tooltip>
                );
              }

              const buttonClassName = [
                "toolbar-button",
                active ? "toolbar-button--active" : "",
                disabled ? "toolbar-button--disabled" : "",
                buttonVariant ? `toolbar-button--${buttonVariant}` : "",
              ]
                .filter(Boolean)
                .join(" ");

              return (
                <Tooltip key={btn.key} content={tooltip}>
                  <span
                    className={disabled ? "toolbar-btn-wrapper toolbar-btn-wrapper--disabled" : "toolbar-btn-wrapper"}
                    onContextMenu={handleRightClick(btn)}>
                    <button
                      id={btn.domId}
                      type="button"
                      className={buttonClassName}
                      disabled={disabled}
                      aria-pressed={active}
                      onClick={disabled ? undefined : btn.onClick}
                      // Most reliable for wheel-click (fires before browser autoscroll kicks in)
                      onMouseDown={disabled ? undefined : handleMiddleClick(btn)}
                      // Some browsers fire auxclick too; harmless redundancy
                      onAuxClick={disabled ? undefined : handleMiddleClick(btn)}>
                      {btn.icon}
                    </button>
                  </span>
                </Tooltip>
              );
            })}
          </div>

          {showCaptions && <div className="toolbar-caption">{group.caption}</div>}
        </div>
      ))}

      {trailing != null && <div className="toolbar-trailing">{trailing}</div>}
    </div>
  );
}

export default ToolBar;
