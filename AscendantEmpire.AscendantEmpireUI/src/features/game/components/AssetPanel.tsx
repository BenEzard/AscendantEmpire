import { type Ref } from "react";
import AssetTickerCollection, { type AssetCategoryData, type AssetTickerData } from "@/features/game/assets/tickers/AssetTickerCollection";

interface AssetPanelProps {
  overlayRef: Ref<HTMLDivElement>;
  top: number;
  left: number;
  categories: AssetCategoryData[];
  /** Called with a group's new ticker order after a drag. The order is the assets' usage order, not a display preference. */
  onReorderGroupTickers: (args: { categoryCode: string; groupCode: string; tickers: AssetTickerData[] }) => void;
  /** Called with an asset code when the user clicks a (non-total) asset amount. */
  onClickAsset: (assetCode: string | null) => void;
}

/** The floating overlay showing one meta-category's asset tickers, anchored under that meta-category's toolbar button. */
export default function AssetPanel({ overlayRef, top, left, categories, onReorderGroupTickers, onClickAsset }: AssetPanelProps) {
  return (
    <div
      ref={overlayRef}
      style={{
        position: "fixed",
        top,
        left,
        transform: undefined,
        zIndex: 1050,
        background: "rgba(255, 255, 255, 0.96)",
        border: "1px solid rgba(0, 0, 0, 0.12)",
        borderRadius: "8px",
        boxShadow: "0 10px 30px rgba(0,0,0,0.2)",
        padding: "0.5rem",
        width: "fit-content",
        maxWidth: "calc(100vw - 2rem)",
      }}>
      <AssetTickerCollection
        categories={categories}
        // A floor, not a cap: a panel holding one group would otherwise be a single narrow column.
        maxColumns={4}
        onReorderGroupTickers={onReorderGroupTickers}
        onClickAmount={({ assetCode, isTotal }) => {
          if (isTotal) {
            return;
          }
          onClickAsset(assetCode ?? null);
        }}
      />
    </div>
  );
}
