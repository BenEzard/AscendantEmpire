import { type Dispatch, type Ref, type SetStateAction } from "react";
import ResourceTickerCollection, { type ResourceCategoryData } from "@/features/game/resources/tickers/ResourceTickerCollection";

interface ConstructionPanelProps {
  overlayRef: Ref<HTMLDivElement>;
  top: number;
  left: number;
  categories: ResourceCategoryData[];
  setCategories: Dispatch<SetStateAction<ResourceCategoryData[]>>;
  /** Called with a resource code when the user clicks a (non-total) resource amount. */
  onClickResource: (resourceCode: string | null) => void;
}

/** The floating construction overlay showing the resource ticker collection. */
export default function ConstructionPanel({ overlayRef, top, left, categories, setCategories, onClickResource }: ConstructionPanelProps) {
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
        maxColumns={4}
        onReorderGroupTickers={({ categoryCode, groupCode, tickers }) => {
          console.log("[GameUI] Ticker reorder:", { categoryCode, groupCode, order: tickers.map((t) => t.name) });
          setCategories((prev) =>
            prev.map((category) => {
              if (category.code !== categoryCode || category.type !== "processed-resources") return category;
              return {
                ...category,
                tickerGroups: category.tickerGroups.map((group) => (group.code === groupCode ? { ...group, tickers } : group)),
              };
            }),
          );
        }}
        onClickAmount={({ resourceCode, isTotal }) => {
          if (isTotal) return;
          onClickResource(resourceCode ?? null);
        }}
      />
    </div>
  );
}
