import { Button, Modal } from "@/components/ui";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  onCommit?: (changes: Record<string, SliderSnapshot[]>) => void;
}

type SectionKey = "general" | "production" | "distribution" | "trade";

const sections: { key: SectionKey; label: string }[] = [
  { key: "general", label: "General" },
  { key: "production", label: "Production" },
  { key: "distribution", label: "Distribution" },
  { key: "trade", label: "Trade" },
];

export default function ResourceManagerDialog({
  tickers,
  selectedResourceId,
  onSelectResource,
  show,
  onClose,
  onCommit,
}: ResourceManagerDialogProps) {
  const [activeSection, setActiveSection] = useState<SectionKey>("general");

  // Accumulates slider snapshots per resource ID across the lifetime of one dialog session.
  // Stored in a ref so slider drags don't trigger re-renders.
  const sliderChangesRef = useRef<Record<string, SliderSnapshot[]>>({});
  const remainderChangesRef = useRef<Record<string, number | null>>({});

  // Reset accumulated changes whenever the dialog opens.
  useEffect(() => {
    if (show) {
      sliderChangesRef.current = {};
      remainderChangesRef.current = {};
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

  const logChanges = useCallback(() => {
    const changedIds = Object.keys(sliderChangesRef.current);
    if (changedIds.length === 0) {
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
    console.groupEnd();
  }, [tickers]);

  const handleClose = useCallback(() => {
    logChanges();
    onClose();
  }, [logChanges, onClose]);

  const handleApply = useCallback(() => {
    logChanges();
    onCommit?.(sliderChangesRef.current);
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
    <Modal
      open={show}
      onClose={onClose}
      title={titleNode}
      size="lg"
      panelClassName="thick-border-modal"
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
      }>
      <div className="grid grid-cols-12 gap-4">
        {/* Left: section tabs */}
        <div className="col-span-12 md:col-span-4">
          <div className="rounded-md border border-slate-200 bg-white">
            {sections.map(({ key, label }, index) => (
              <button
                key={key}
                type="button"
                onClick={() => setActiveSection(key)}
                className={[
                  "block w-full px-3 py-2 text-left text-sm",
                  index > 0 ? "border-t border-slate-200" : "",
                  activeSection === key ? "nav-item-selected font-semibold" : "hover:bg-slate-50",
                ]
                  .filter(Boolean)
                  .join(" ")}>
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* Right: section content — all sections rendered in the same grid cell so the
            container always matches the tallest section and never changes size. */}
        <div className="col-span-12 md:col-span-8">
          <div className="grid">
            <div style={{ gridArea: "1/1" }} className={activeSection !== "general" ? "invisible" : ""}>
              <ResourceManagerGeneralPage selectedTicker={selectedTicker} />
            </div>
            <div style={{ gridArea: "1/1" }} className={activeSection !== "production" ? "invisible" : ""}>
              <ResourceManagerProductionPage
                selectedTicker={selectedTicker}
                totalThisTurn={selectedTicker?.resourcesAddedPerTurn ?? 0}
                totalLastTurn={0}
              />
            </div>
            <div style={{ gridArea: "1/1" }} className={activeSection !== "distribution" ? "invisible" : ""}>
              <ResourceManagerDistributionPage
                selectedTicker={selectedTicker}
                resourcesPerTurn={selectedTicker?.resourcesAddedPerTurn || 800}
                amountLastTurn={0}
                currentConsumption={0}
                maxConsumption={0}
                onSliderChange={handleSliderChange}
                onRemainderChange={handleRemainderChange}
              />
            </div>
            <div style={{ gridArea: "1/1" }} className={activeSection !== "trade" ? "invisible" : ""}>
              <ResourceManagerTradePage selectedTicker={selectedTicker} />
            </div>
          </div>
        </div>
      </div>
    </Modal>
  );
}
