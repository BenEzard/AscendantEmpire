import { Button, PagedDialog } from "@/components/ui";
import type { SelectionOption } from "@/components/ui";
import { useCallback, useEffect, useMemo, useRef } from "react";
import type { AssetTickerData } from "../tickers/AssetTickerCollection";
import type { SliderSnapshot } from "../slider";
import AssetManagerGeneralPage from "./AssetManagerGeneralPage";
import AssetManagerProductionPage from "./AssetManagerProductionPage";
import AssetManagerDistributionPage from "./AssetManagerDistributionPage";
import AssetManagerTradePage from "./AssetManagerTradePage";

interface AssetManagerDialogProps {
  tickers: AssetTickerData[];
  selectedAssetId: string | null;
  onSelectAsset: (id: string | null) => void;
  show: boolean;
  onClose: () => void;
  onCommit?: (changes: Record<string, SliderSnapshot[]>, tradePartners: Record<string, SelectionOption[]>) => void;
}

export default function AssetManagerDialog({
  tickers,
  selectedAssetId,
  onSelectAsset,
  show,
  onClose,
  onCommit,
}: AssetManagerDialogProps) {
  // Accumulates slider snapshots per asset ID across the lifetime of one dialog session.
  // Stored in a ref so slider drags don't trigger re-renders.
  const sliderChangesRef = useRef<Record<string, SliderSnapshot[]>>({});
  const remainderChangesRef = useRef<Record<string, number | null>>({});
  const tradePartnersRef = useRef<Record<string, SelectionOption[]>>({});

  // Reset accumulated changes whenever the dialog opens.
  useEffect(() => {
    if (show) {
      sliderChangesRef.current = {};
      remainderChangesRef.current = {};
      tradePartnersRef.current = {};
    }
  }, [show]);

  const handleSliderChange = useCallback(
    (sliders: SliderSnapshot[]) => {
      if (!selectedAssetId) return;
      sliderChangesRef.current = { ...sliderChangesRef.current, [selectedAssetId]: sliders };
    },
    [selectedAssetId],
  );

  const handleRemainderChange = useCallback(
    (sliderId: number | null) => {
      if (!selectedAssetId) return;
      remainderChangesRef.current = { ...remainderChangesRef.current, [selectedAssetId]: sliderId };
    },
    [selectedAssetId],
  );

  const handleTradePartnersChange = useCallback(
    (partners: SelectionOption[]) => {
      if (!selectedAssetId) return;
      tradePartnersRef.current = { ...tradePartnersRef.current, [selectedAssetId]: partners };
    },
    [selectedAssetId],
  );

  const logChanges = useCallback(() => {
    const changedIds = Object.keys(sliderChangesRef.current);
    const tradeChangedIds = Object.keys(tradePartnersRef.current);
    if (changedIds.length === 0 && tradeChangedIds.length === 0) {
      console.log("[AssetManager] No changes recorded.");
      return;
    }
    console.group("[AssetManager] Changes on close:");
    for (const assetId of changedIds) {
      const ticker = tickers.find((t) => t.code === assetId);
      console.group(ticker?.name ?? assetId);
      console.log("Sliders:", sliderChangesRef.current[assetId]);
      const remainder = remainderChangesRef.current[assetId];
      if (remainder !== undefined) console.log("Remainder slider ID:", remainder);
      console.groupEnd();
    }
    for (const assetId of tradeChangedIds) {
      if (changedIds.includes(assetId)) continue; // already logged above
      const ticker = tickers.find((t) => t.code === assetId);
      console.group(ticker?.name ?? assetId);
      console.log("Trade partners:", tradePartnersRef.current[assetId]);
      console.groupEnd();
    }
    if (tradeChangedIds.length > 0) {
      console.log("Trade partners (all):", tradePartnersRef.current);
    }
    console.groupEnd();
  }, [tickers]);

  const handleClose = useCallback(() => {
    logChanges();
    onClose();
  }, [logChanges, onClose]);

  const handleApply = useCallback(() => {
    logChanges();
    onCommit?.(sliderChangesRef.current, tradePartnersRef.current);
    onClose();
  }, [logChanges, onCommit, onClose]);

  const orderedTickers = useMemo(() => tickers.slice().sort((a, b) => a.name.localeCompare(b.name)), [tickers]);

  const selectedTicker = tickers.find((t) => t.code === selectedAssetId) ?? null;

  const titleNode = (
    <div className="flex items-center gap-3">
      <span>Resource Manager</span>
      <select
        className="w-56 rounded-md border border-slate-300 px-2 py-1 text-sm font-normal"
        value={selectedTicker?.code ?? ""}
        onChange={(e) => {
          const raw = e.target.value;
          onSelectAsset(raw === "" ? null : raw);
        }}>
        <option value="">— Select —</option>
        {orderedTickers.map((t) => (
          <option key={t.code} value={t.code}>
            {t.name}
          </option>
        ))}
      </select>
    </div>
  );

  return (
    <PagedDialog
      open={show}
      title={titleNode}
      size="lg"
      panelClassName="thick-border-modal"
      onCancel={handleClose}
      onCommit={onCommit ? handleApply : undefined}
      footer={
        <>
          {onCommit && (
            <Button variant="primary" onClick={handleApply}>
              Apply
            </Button>
          )}
          <Button variant="secondary" onClick={handleClose}>
            Close
          </Button>
        </>
      }
      sections={[
        {
          key: "general",
          label: "General",
          content: <AssetManagerGeneralPage selectedTicker={selectedTicker} />,
        },
        {
          key: "production",
          label: "Production",
          content: (
            <AssetManagerProductionPage
              selectedTicker={selectedTicker}
              totalThisTurn={selectedTicker?.amountAddedPerTurn || 800}
              totalLastTurn={0}
              currentConsumption={0}
              maxConsumption={0}
            />
          ),
        },
        {
          key: "distribution",
          label: "Distribution",
          content: (
            <AssetManagerDistributionPage
              selectedTicker={selectedTicker}
              assetsPerTurn={selectedTicker?.amountAddedPerTurn || 800}
              amountLastTurn={0}
              currentConsumption={0}
              maxConsumption={0}
              onSliderChange={handleSliderChange}
              onRemainderChange={handleRemainderChange}
            />
          ),
        },
        {
          key: "trade",
          label: "Trade",
          content: (
            <AssetManagerTradePage
              selectedTicker={selectedTicker}
              assetsPerTurn={selectedTicker?.amountAddedPerTurn || 800}
              amountLastTurn={0}
              currentConsumption={0}
              maxConsumption={0}
              onTradePartnersChange={handleTradePartnersChange}
            />
          ),
        },
      ]}
    />
  );
}
