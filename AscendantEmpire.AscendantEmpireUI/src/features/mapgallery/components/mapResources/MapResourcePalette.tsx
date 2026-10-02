import { FaCheck } from "react-icons/fa6";
import type { MapResourceCategory } from "@/types/map/MapResource";
import MapResourceIcon from "./MapResourceIcon";

interface MapResourcePaletteProps {
  categories: MapResourceCategory[];
  selectedTypeId: number | null;
  /** Called with the chosen type's id, or null for "<none>". */
  onSelect: (typeId: number | null) => void;
  /** Closes the dropdown the palette sits in. */
  close: () => void;
}

/**
 * The MapResources palette's dropdown: every saved MapResourceType, grouped under its category in columns, laid out after the in-game asset
 * collection (a heading per category over its entries). Choosing one arms the MapResource tool with it.
 */
export default function MapResourcePalette({ categories, selectedTypeId, onSelect, close }: MapResourcePaletteProps) {
  // A category with no saved types has nothing to place, so it is left out rather than shown empty.
  const columns = categories.filter((category) => category.types.some((type) => type.id != null));

  const choose = (typeId: number | null) => {
    onSelect(typeId);
    close();
  };

  return (
    <div className="p-2" role="group" aria-label="MapResources">
      <button
        type="button"
        role="menuitem"
        className={`mb-2 flex w-full items-center gap-2 rounded px-2 py-1 text-left text-sm hover:bg-slate-100 ${selectedTypeId == null ? "bg-slate-100" : ""}`}
        onClick={() => choose(null)}>
        <span className="inline-block h-5 w-5" />
        <span>{"<none>"}</span>
        {selectedTypeId == null ? <FaCheck aria-hidden size={12} className="text-slate-600" /> : null}
      </button>

      {columns.length === 0 ? (
        <p className="px-2 py-1 text-xs text-slate-500">No Map Resources yet. Click the button itself to open the Map Resource Type Manager.</p>
      ) : (
        <div className="flex max-h-96 gap-4 overflow-auto">
          {columns.map((category) => (
            <section key={category.code} className="min-w-36" aria-label={category.displayName}>
              <h3 className="mb-1 border-b border-slate-200 px-1 pb-0.5 text-xs font-semibold uppercase tracking-wide text-slate-500">{category.displayName}</h3>
              <ul className="space-y-0.5">
                {category.types
                  .filter((type) => type.id != null)
                  .map((type) => {
                    const active = type.id === selectedTypeId;

                    return (
                      <li key={type.id}>
                        <button
                          type="button"
                          role="menuitem"
                          className={`flex w-full items-center gap-2 rounded px-1 py-1 text-left text-sm hover:bg-slate-100 ${active ? "bg-slate-100 font-medium" : ""}`}
                          onClick={() => choose(type.id!)}>
                          <MapResourceIcon iconPath={type.iconPath} size={20} />
                          <span className="whitespace-nowrap">{type.displayName}</span>
                          {active ? <FaCheck aria-hidden size={12} className="text-slate-600" /> : null}
                        </button>
                      </li>
                    );
                  })}
              </ul>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
