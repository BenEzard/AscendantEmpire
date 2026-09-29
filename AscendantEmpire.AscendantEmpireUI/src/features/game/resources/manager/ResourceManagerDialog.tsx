import { Button, PagedDialog } from "@/components/ui";
import type { SelectionOption } from "@/components/ui";
import { useCallback, useEffect, useMemo, useRef } from "react";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import type { SliderSnapshot } from "../slider";
import ResourceManagerGeneralPage from "./ResourceManagerGeneralPage";
import ResourceManagerProductionPage from "./ResourceManagerProductionPage";
import ResourceManagerDistributionPage from "./ResourceManagerDistributionPage";
import ResourceManagerTradePage from "./ResourceManagerTradePage";

interface ResourceManagerDialogProps {
  tickers: ResourceTickerData[];
  selectedResourceId: string | null;
  onSelectResource: (id: string | null) => void;
  show: boolean;
  onClose: () => void;
  onCommit?: (changes: Record<string, SliderSnapshot[]>, tradePartners: Record<string, SelectionOption[]>) => void;
}

export default function ResourceManagerDialog({
  tickers,
  selectedResourceId,
  onSelectResource,
  show,
  onClose,
  onCommit,
}: ResourceManagerDialogProps) {
  // Accumulates slider snapshots per resource ID across the lifetime of one dialog session.
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
      if (!selectedResourceId) return;
      sliderChangesRef.current = { ...sliderChangesRef.current, [selectedResourceId]: sliders };
    },
    [selectedResourceId],
  );

  const handleRemainderChange = useCallback(
    (sliderId: number | null) => {
      if (!selectedResourceId) return;
      remainderChangesRef.current = { ...remainderChangesRef.current, [selectedResourceId]: sliderId };
    },
    [selectedResourceId],
  );

  const handleTradePartnersChange = useCallback(
    (partners: SelectionOption[]) => {
      if (!selectedResourceId) return;
      tradePartnersRef.current = { ...tradePartnersRef.current, [selectedResourceId]: partners };
    },
    [selectedResourceId],
  );

  const logChanges = useCallback(() => {
    const changedIds = Object.keys(sliderChangesRef.current);
    const tradeChangedIds = Object.keys(tradePartnersRef.current);
    if (changedIds.length === 0 && tradeChangedIds.length === 0) {
      console.log("[ResourceManager] No changes recorded.");
      return;
    }
    console.group("[ResourceManager] Changes on close:");
    for (const resourceId of changedIds) {
      const ticker = tickers.find((t) => t.code === resourceId);
      console.group(ticker?.name ?? resourceId);
      console.log("Sliders:", sliderChangesRef.current[resourceId]);
      const remainder = remainderChangesRef.current[resourceId];
      if (remainder !== undefined) console.log("Remainder slider ID:", remainder);
      console.groupEnd();
    }
    for (const resourceId of tradeChangedIds) {
      if (changedIds.includes(resourceId)) continue; // already logged above
      const ticker = tickers.find((t) => t.code === resourceId);
      console.group(ticker?.name ?? resourceId);
      console.log("Trade partners:", tradePartnersRef.current[resourceId]);
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

  const selectedTicker = tickers.find((t) => t.code === selectedResourceId) ?? null;

  const titleNode = (
    <div className="flex items-center gap-3">
      <span>Resource Manager</span>
      <select
        className="w-56 rounded-md border border-slate-300 px-2 py-1 text-sm font-normal"
        value={selectedTicker?.code ?? ""}
        onChange={(e) => {
          const raw = e.target.value;
          onSelectResource(raw === "" ? null : raw);
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
          content: <ResourceManagerGeneralPage selectedTicker={selectedTicker} />,
        },
        {
          key: "production",
          label: "Production",
          content: (
            <ResourceManagerProductionPage
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
            <ResourceManagerDistributionPage
              selectedTicker={selectedTicker}
              resourcesPerTurn={selectedTicker?.amountAddedPerTurn || 800}
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
            <ResourceManagerTradePage
              selectedTicker={selectedTicker}
              resourcesPerTurn={selectedTicker?.amountAddedPerTurn || 800}
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
