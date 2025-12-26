import React from "react";
import { Button, ButtonGroup, OverlayTrigger, Tooltip } from "react-bootstrap";
import "./Toolbar.css";

export interface ToolBarButton {
  key: string;
  icon?: React.ReactNode;
  tooltip?: string;
  onClick?: () => void;
  onMiddleClick?: () => void;
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
    if (!btn.onMiddleClick) return;

    // Stop browser autoscroll / other default behaviors
    e.preventDefault();
    e.stopPropagation();

    btn.onMiddleClick();
  };

  return (
    <div className="toolbar d-flex justify-content-center border-bottom bg-light">
      {groups.map((group) => (
        <div key={group.caption} className="toolbar-group">
          <ButtonGroup aria-label={group.caption}>
            {group.buttons.map((btn) => {
              const tooltip = btn.onMiddleClick && btn.tooltip ? `${btn.tooltip} (middle-click: add new)` : btn.tooltip;

              return (
                <OverlayTrigger key={btn.key} placement="bottom" overlay={<Tooltip id={`tooltip-${btn.key}`}>{tooltip}</Tooltip>}>
                  <Button
                    variant="secondary"
                    onClick={btn.onClick}
                    // Most reliable for wheel-click (fires before browser autoscroll kicks in)
                    onMouseDown={handleMiddleClick(btn)}
                    // Some browsers fire auxclick too; harmless redundancy
                    onAuxClick={handleMiddleClick(btn)}>
                    {btn.icon}
                  </Button>
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
