import { useEffect, useMemo, useState } from "react";
import ComponentCard from "./ComponentCard";
import ComponentProperty from "./ComponentProperty";
import { SlClose } from "react-icons/sl";

import type { UnitComponent } from "./types/UnitComponent";
import { labelForUnitComponentType } from "./types/UnitComponent";
import type { UnitComponentType } from "./types/UnitComponentType";
import type { Domain } from "@/features/unitdesign/types/Domain";

export interface ComponentSelectionWindowProps {
  open: boolean;
  componentId: number;
  type: UnitComponentType;
  base?: Extract<UnitComponent, { type: "base" }> | null;
  selectedComponentId?: number | null;
  components: UnitComponent[];
  onClose: () => void;
  onConfirm: (args: { selectedComponentId: number | null; selectedName: string | null }) => void;
}

export default function ComponentSelectionWindow({
  open,
  componentId: _slotComponentId,
  type,
  selectedComponentId,
  components,
  onClose,
  onConfirm,
}: ComponentSelectionWindowProps) {
  const [query, setQuery] = useState("");
  const [activeSelectedComponentId, setActiveSelectedComponentId] = useState<number | null>(selectedComponentId ?? null);

  const domainOptions: (Domain | "All")[] = ["All", "Land", "Water", "Below Water", "Air", "Near Earth Orbit"];

  const [domain, setDomain] = useState<Domain | "All">("All"); // or "Land" | "" if you want an "All" option

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();

    return components.filter((c) => (q ? c.name.toLowerCase().includes(q) : true)).filter((c) => domain === "All" || c.domain === domain); // adjust to your data shape
  }, [components, query, domain]);

  useEffect(() => {
    if (!open) return;
    setActiveSelectedComponentId(selectedComponentId ?? null);
  }, [open, selectedComponentId]);

  const currentSlotSelectionName = useMemo(() => {
    if (selectedComponentId == null) return null;
    return components.find((c) => c.id === selectedComponentId)?.name ?? null;
  }, [components, selectedComponentId]);

  const selectedComponent = useMemo(() => {
    if (activeSelectedComponentId == null) return null;
    return components.find((c) => c.id === activeSelectedComponentId) ?? null;
  }, [activeSelectedComponentId, components]);

  const existingSelectionComponent = useMemo(() => {
    if (selectedComponentId == null) return null;
    return components.find((c) => c.id === selectedComponentId) ?? null;
  }, [components, selectedComponentId]);

  if (!open) return null;

  return (
    <div className="absolute inset-0 z-50 bg-sky-800 text-black">
      <div className="flex h-full w-full flex-col">
        <div className="flex items-center gap-3 border-b border-slate-200 px-4 py-3">
          <div className="shrink-0 text-lg font-semibold text-white">{labelForUnitComponentType(type)} Selection</div>

          <input
            className="h-9 w-full rounded-md border border-slate-300 bg-slate-200 px-3 text-sm outline-none focus:ring-2 focus:ring-slate-400"
            placeholder="Search..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="Search components"
          />

          {/* Domain dropdown */}
          <label className="shrink-0 flex items-center gap-2 text-sm text-white">
            <span className="whitespace-nowrap">Domain</span>
            <select
              className="h-9 rounded-md border border-slate-300 bg-slate-200 px-2 text-sm text-black outline-none focus:ring-2 focus:ring-slate-400"
              value={domain}
              onChange={(e) => setDomain(e.target.value as Domain)}
              aria-label="Domain">
              {domainOptions.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </label>

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
                  key={c.id}
                  name={c.name}
                  properties={c.properties}
                  icon={(() => {
                    const Icon = c.icon;
                    return Icon ? <Icon className="h-10 w-10 text-slate-700" aria-hidden /> : undefined;
                  })()}
                  imageUrl={c.iconUrl}
                  imageAlt={c.iconUrl ? c.name : undefined}
                  selected={activeSelectedComponentId === c.id}
                  onClick={() => setActiveSelectedComponentId(c.id)}
                  onDoubleClick={() => {
                    onConfirm({ selectedComponentId: c.id, selectedName: c.name });
                    onClose();
                  }}
                />
              ))}

              {type !== "base" && (
                <ComponentCard
                  key="__none__"
                  name="No Selection"
                  properties={{}}
                  icon={<SlClose className="h-8 w-8 text-slate-600" aria-hidden />}
                  selected={activeSelectedComponentId == null}
                  onClick={() => {
                    setActiveSelectedComponentId(null);
                    onConfirm({ selectedComponentId: null, selectedName: null });
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
