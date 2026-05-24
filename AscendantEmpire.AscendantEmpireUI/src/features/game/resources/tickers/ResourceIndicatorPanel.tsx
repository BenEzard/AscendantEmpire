import ResourceIndicator from "./ResourceIndicator";
import { RESOURCE_INDICATOR_COLORS } from "./ResourceConstants";

export interface ResourceIndicatorPanelProps {
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicStockpile?: boolean;
}

export default function ResourceIndicatorPanel({
  isImported = false,
  isExported = false,
  hasStrategicStockpile = false,
}: ResourceIndicatorPanelProps) {
  return (
    <span className="resource-indicator-panel" aria-label="Resource state indicators">
      <ResourceIndicator
        active={isImported}
        activeColor={RESOURCE_INDICATOR_COLORS.imported}
        inactiveColor={RESOURCE_INDICATOR_COLORS.inactive}
        label={isImported ? "Imported" : "Not imported"}
      />
      <ResourceIndicator
        active={isExported}
        activeColor={RESOURCE_INDICATOR_COLORS.exported}
        inactiveColor={RESOURCE_INDICATOR_COLORS.inactive}
        label={isExported ? "Exported" : "Not exported"}
      />
      <ResourceIndicator
        active={hasStrategicStockpile}
        activeColor={RESOURCE_INDICATOR_COLORS.strategicStockpile}
        inactiveColor={RESOURCE_INDICATOR_COLORS.inactive}
        label={hasStrategicStockpile ? "Strategic stockpile available" : "No strategic stockpile"}
      />
    </span>
  );
}
