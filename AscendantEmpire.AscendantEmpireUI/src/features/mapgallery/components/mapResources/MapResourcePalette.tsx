import { FaCheck, FaTree } from "react-icons/fa6";
import type { IconType } from "react-icons";
import type { MapResourceCategory } from "@/types/map/MapResource";
import MapResourceIcon from "@/components/map/MapResourceIcon";

/**
 * Palette glyphs for the types that carry no image of their own, keyed by MapResourceType code.
 *
 * A type the Biome already says — a forest's Timber — draws nothing on a tile, but the palette still has to offer it, and an entry with no label is
 * only as findable as what it shows. A type listed nowhere here falls back to the first letters of its name.
 */
const PALETTE_GLYPHS: Record<string, IconType> = {
  TIMBER: FaTree,
};

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
 * collection (a heading per category over a wrapping grid of its types). A type is its icon alone, named by the entry's tooltip. Choosing one arms
 * the MapResource tool with it.
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
        className={`mb-2 flex w-full items-center gap-2 rounded px-2 py-1 text-left text-sm text-slate-800 hover:bg-slate-100 ${selectedTypeId == null ? "bg-slate-100" : ""}`}
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
              <ul className="flex flex-wrap gap-1">
                {category.types
                  .filter((type) => type.id != null)
                  .map((type) => {
                    const active = type.id === selectedTypeId;
                    const Glyph = type.iconPath ? null : PALETTE_GLYPHS[type.code.toUpperCase()];

                    return (
                      <li key={type.id}>
                        <button
                          type="button"
                          role="menuitem"
                          title={type.displayName}
                          aria-label={type.displayName}
                          className={`flex h-8 w-8 items-center justify-center rounded text-slate-700 hover:bg-slate-100 ${active ? "bg-slate-100 ring-2 ring-slate-400" : ""}`}
                          onClick={() => choose(type.id!)}>
                          {type.iconPath ? (
                            <MapResourceIcon iconPath={type.iconPath} size={20} />
                          ) : Glyph ? (
                            <Glyph aria-hidden size={20} />
                          ) : (
                            <span aria-hidden className="text-[10px] font-semibold uppercase text-slate-500">{type.displayName.slice(0, 2)}</span>
                          )}
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
