import { useEffect, useMemo, useState } from "react";
import ComponentCard from "./ComponentCard";
import ComponentProperty from "./ComponentProperty";
import { SlClose } from "react-icons/sl";

import type { UnitComponent, DomainType } from "./types/UnitComponent";
import { DOMAIN_TYPES } from "./types/UnitComponent";

export interface ComponentSelectionWindowProps {
  open: boolean;
  /** Heading, e.g. "Base Selection" or the slot's label. */
  title: string;
  /** True when choosing the base body (enables the domain filter, hides the "No Selection" card). */
  isBaseSlot: boolean;
  /** Candidate components already filtered to those the slot accepts. */
  components: UnitComponent[];
  /** Code of the component currently fitted in this slot, if any. */
  selectedCode?: string | null;
  onClose: () => void;
  onConfirm: (args: { selectedCode: string | null; selectedName: string | null }) => void;
}

export default function ComponentSelectionWindow({
  open,
  title,
  isBaseSlot,
  components,
  selectedCode,
  onClose,
  onConfirm,
}: ComponentSelectionWindowProps) {
  const [query, setQuery] = useState("");
  const [activeSelectedCode, setActiveSelectedCode] = useState<string | null>(selectedCode ?? null);

  const domainOptions: (DomainType | "All")[] = ["All", ...DOMAIN_TYPES];
  const [domain, setDomain] = useState<DomainType | "All">("All");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();

    return components
      .filter((c) => (q ? c.displayName.toLowerCase().includes(q) : true))
      // The domain filter only applies to bases (bodies carry a movement domain); other components have none.
      .filter((c) => !isBaseSlot || domain === "All" || c.body?.movementDomain === domain);
  }, [components, query, domain, isBaseSlot]);

  useEffect(() => {
    if (!open) return;
    setActiveSelectedCode(selectedCode ?? null);
  }, [open, selectedCode]);

  const currentSlotSelectionName = useMemo(() => {
    if (selectedCode == null) return null;
    return components.find((c) => c.code === selectedCode)?.displayName ?? null;
  }, [components, selectedCode]);

  const selectedComponent = useMemo(() => {
    if (activeSelectedCode == null) return null;
    return components.find((c) => c.code === activeSelectedCode) ?? null;
  }, [activeSelectedCode, components]);

  const existingSelectionComponent = useMemo(() => {
    if (selectedCode == null) return null;
    return components.find((c) => c.code === selectedCode) ?? null;
  }, [components, selectedCode]);

  if (!open) return null;

  return (
    <div className="absolute inset-0 z-50 bg-sky-800 text-black">
      <div className="flex h-full w-full flex-col">
        <div className="flex items-center gap-3 border-b border-slate-200 px-4 py-3">
          <div className="shrink-0 text-lg font-semibold text-white">{title}</div>

          <input
            className="h-9 w-full rounded-md border border-slate-300 bg-slate-200 px-3 text-sm outline-none focus:ring-2 focus:ring-slate-400"
            placeholder="Search..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="Search components"
          />

          {/* Domain dropdown — only meaningful when picking a base body. */}
          {isBaseSlot && (
            <label className="shrink-0 flex items-center gap-2 text-sm text-white">
              <span className="whitespace-nowrap">Domain</span>
              <select
                className="h-9 rounded-md border border-slate-300 bg-slate-200 px-2 text-sm text-black outline-none focus:ring-2 focus:ring-slate-400"
                value={domain}
                onChange={(e) => setDomain(e.target.value as DomainType | "All")}
                aria-label="Domain">
                {domainOptions.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </label>
          )}

          {currentSlotSelectionName && (
            <div className="shrink-0 text-xs text-white/70 leading-tight">
              <div className="whitespace-nowrap">Current Selection:</div>
              <div className="whitespace-nowrap font-semibold text-white/80">{currentSlotSelectionName}</div>
            </div>
          )}

          <button
            type="button"
            className="shrink-0 rounded-md px-2 py-1 hover:bg-white/15 text-white focus:outline-none focus-visible:ring-2 focus-visible:ring-white/30"
            aria-label="Close"
            onClick={onClose}>
            ×
          </button>
        </div>

        <div className="flex flex-1 min-h-0">
          <div className="flex-1 min-w-0 overflow-auto px-4 py-4">
            <div className="grid gap-2 [grid-template-columns:repeat(auto-fill,minmax(160px,1fr))]">
              {filtered.map((c) => (
                <ComponentCard
                  key={c.code}
                  name={c.displayName}
                  imageUrl={c.designerImagePath ?? undefined}
                  imageAlt={c.designerImagePath ? c.displayName : undefined}
                  selected={activeSelectedCode === c.code}
                  onClick={() => setActiveSelectedCode(c.code)}
                  onDoubleClick={() => {
                    onConfirm({ selectedCode: c.code, selectedName: c.displayName });
                    onClose();
                  }}
                />
              ))}

              {!isBaseSlot && (
                <ComponentCard
                  key="__none__"
                  name="No Selection"
                  icon={<SlClose className="h-8 w-8 text-slate-600" aria-hidden />}
                  selected={activeSelectedCode == null}
                  onClick={() => {
                    setActiveSelectedCode(null);
                    onConfirm({ selectedCode: null, selectedName: null });
                    onClose();
                  }}
                />
              )}
            </div>
          </div>

          <div className="basis-1/5 shrink-0">
            <ComponentProperty selectedComponent={selectedComponent} existingSelection={existingSelectionComponent} />
          </div>
        </div>
      </div>
    </div>
  );
}
