import React from "react";
import { Button, ButtonGroup, OverlayTrigger, Tooltip } from "react-bootstrap";
import "./Toolbar.css";

export interface ToolBarButton {
  key: string;
  icon?: React.ReactNode;
  tooltip?: string;
  onClick?: () => void;
}

export interface ToolBarGroup {
  caption: string;
  buttons: ToolBarButton[];
}

interface ToolBarProps {
  groups: ToolBarGroup[];
}

function ToolBar({ groups }: ToolBarProps) {
  return (
    <div className="toolbar d-flex justify-content-center border-bottom bg-light">
      {groups.map((group) => (
        <div key={group.caption} className="toolbar-group">
          <ButtonGroup aria-label={group.caption}>
            {group.buttons.map((btn) => (
              <OverlayTrigger key={btn.key} placement="bottom" overlay={<Tooltip id={`tooltip-${btn.key}`}>{btn.tooltip}</Tooltip>}>
                <Button variant="secondary" onClick={btn.onClick}>
                  {btn.icon}
                </Button>
              </OverlayTrigger>
            ))}
          </ButtonGroup>
          <div className="toolbar-caption">{group.caption}</div>
        </div>
      ))}
    </div>
  );
}

export default ToolBar;
