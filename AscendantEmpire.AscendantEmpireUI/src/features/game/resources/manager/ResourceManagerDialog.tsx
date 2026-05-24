import { Button, Modal } from "@/components/ui";
import { useMemo, useState } from "react";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
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
}

type SectionKey = "general" | "production" | "distribution" | "trade";

const sections: { key: SectionKey; label: string }[] = [
  { key: "general", label: "General" },
  { key: "production", label: "Production" },
  { key: "distribution", label: "Distribution" },
  { key: "trade", label: "Trade" },
];

export default function ResourceManagerDialog({ tickers, selectedResourceId, onSelectResource, show, onClose }: ResourceManagerDialogProps) {
  const [activeSection, setActiveSection] = useState<SectionKey>("general");

  const orderedTickers = useMemo(() => tickers.slice().sort((a, b) => a.name.localeCompare(b.name)), [tickers]);

  const selectedTicker = tickers.find((t) => t.id === selectedResourceId) ?? null;

  const titleNode = (
    <div className="flex items-center gap-3">
      <span>Resource Manager</span>
      <select
        className="w-56 rounded-md border border-slate-300 px-2 py-1 text-sm font-normal"
        value={selectedTicker?.id ?? ""}
        onChange={(e) => {
          const raw = e.target.value;
          onSelectResource(raw === "" ? null : raw);
        }}>
        <option value="">— Select —</option>
        {orderedTickers.map((t) => (
          <option key={t.id} value={t.id}>
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
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
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
