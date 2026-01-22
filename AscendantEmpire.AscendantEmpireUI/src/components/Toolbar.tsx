import React from "react";
import { Button, ButtonGroup, OverlayTrigger, Tooltip } from "react-bootstrap";
import "./Toolbar.css";

export interface ToolBarButton {
  key: string;
  icon?: React.ReactNode;
  tooltip?: string;
  disabled?: boolean;
  active?: boolean;
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
}

function ToolBar({ groups }: ToolBarProps) {
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
    <div className="toolbar d-flex justify-content-center border-bottom bg-light">
      {groups.map((group) => (
        <div key={group.caption} className="toolbar-group">
          <ButtonGroup aria-label={group.caption}>
            {group.buttons.map((btn) => {
              const middleHint = btn.middleClickHint ?? "add new";
              const tooltip = btn.onMiddleClick && btn.tooltip ? `${btn.tooltip} (middle-click: ${middleHint})` : btn.tooltip;
              const disabled = !!btn.disabled;
              const active = !!btn.active;

              return (
                <OverlayTrigger key={btn.key} placement="bottom" overlay={<Tooltip id={`tooltip-${btn.key}`}>{tooltip}</Tooltip>}>
                  {/*
                    Bootstrap/React-Bootstrap: disabled buttons don't trigger mouse events, so tooltips won't show.
                    Wrap in a span and disable pointer events on the button to keep tooltips working.
                  */}
                  <span
                    className={disabled ? "toolbar-btn-wrapper toolbar-btn-wrapper--disabled" : "toolbar-btn-wrapper"}
                    onContextMenu={handleRightClick(btn)}>
                    <Button
                      variant="secondary"
                      disabled={disabled}
                      active={active}
                      aria-pressed={active}
                      onClick={disabled ? undefined : btn.onClick}
                      // Most reliable for wheel-click (fires before browser autoscroll kicks in)
                      onMouseDown={disabled ? undefined : handleMiddleClick(btn)}
                      // Some browsers fire auxclick too; harmless redundancy
                      onAuxClick={disabled ? undefined : handleMiddleClick(btn)}>
                      {btn.icon}
                    </Button>
                  </span>
                </OverlayTrigger>
              );
            })}
          </ButtonGroup>

          <div className="toolbar-caption">{group.caption}</div>
        </div>
      ))}
    </div>
  );
}

export default ToolBar;
