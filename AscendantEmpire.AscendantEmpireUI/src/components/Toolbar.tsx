import React from "react";
import "./Toolbar.css";

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
  containerClassName?: string;
  showCaptions?: boolean;
  buttonVariant?: string;
}

function ToolBar({
  groups,
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
                  <span key={btn.key} title={tooltip} className={"toolbar-btn-wrapper"} onContextMenu={handleRightClick(btn)}>
                    {btn.render({ disabled, active, tooltip })}
                  </span>
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
                <span
                  key={btn.key}
                  title={tooltip}
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
              );
            })}
          </div>

          {showCaptions && <div className="toolbar-caption">{group.caption}</div>}
        </div>
      ))}
    </div>
  );
}

export default ToolBar;
