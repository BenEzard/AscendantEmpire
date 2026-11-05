import AssetIndicator from "./AssetIndicator";
import { ASSET_INDICATOR_COLORS } from "./AssetConstants";

export interface AssetIndicatorPanelProps {
  isImported?: boolean;
  isExported?: boolean;
  hasStrategicReserve?: boolean;
}

export default function AssetIndicatorPanel({
  isImported = false,
  isExported = false,
  hasStrategicReserve = false,
}: AssetIndicatorPanelProps) {
  return (
    <span className="asset-indicator-panel" aria-label="Resource state indicators">
      <AssetIndicator
        active={isImported}
        activeColor={ASSET_INDICATOR_COLORS.imported}
        inactiveColor={ASSET_INDICATOR_COLORS.inactive}
        label={isImported ? "Imported" : "Not imported"}
        shape="triangle-down"
      />
      <AssetIndicator
        active={isExported}
        activeColor={ASSET_INDICATOR_COLORS.exported}
        inactiveColor={ASSET_INDICATOR_COLORS.inactive}
        label={isExported ? "Exported" : "Not exported"}
        shape="triangle-up"
      />
      <AssetIndicator
        active={hasStrategicReserve}
        activeColor={ASSET_INDICATOR_COLORS.strategicReserve}
        inactiveColor={ASSET_INDICATOR_COLORS.inactive}
        label={hasStrategicReserve ? "Strategic reserve available" : "No strategic reserve"}
      />
    </span>
  );
}
