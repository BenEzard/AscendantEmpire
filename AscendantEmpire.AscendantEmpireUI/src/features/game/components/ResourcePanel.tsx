import { type Ref } from "react";
import ResourceTickerCollection, { type ResourceCategoryData, type ResourceTickerData } from "@/features/game/resources/tickers/ResourceTickerCollection";

interface ResourcePanelProps {
  overlayRef: Ref<HTMLDivElement>;
  top: number;
  left: number;
  categories: ResourceCategoryData[];
  /** Called with a group's new ticker order after a drag. The order is the resources' usage order, not a display preference. */
  onReorderGroupTickers: (args: { categoryCode: string; groupCode: string; tickers: ResourceTickerData[] }) => void;
  /** Called with a resource code when the user clicks a (non-total) resource amount. */
  onClickResource: (resourceCode: string | null) => void;
}

/** The floating overlay showing one meta-category's resource tickers, anchored under that meta-category's toolbar button. */
export default function ResourcePanel({ overlayRef, top, left, categories, onReorderGroupTickers, onClickResource }: ResourcePanelProps) {
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
      <ResourceTickerCollection
        categories={categories}
        // A floor, not a cap: a panel holding one group would otherwise be a single narrow column.
        maxColumns={4}
        onReorderGroupTickers={onReorderGroupTickers}
        onClickAmount={({ resourceCode, isTotal }) => {
          if (isTotal) {
            return;
          }
          onClickResource(resourceCode ?? null);
        }}
      />
    </div>
  );
}
