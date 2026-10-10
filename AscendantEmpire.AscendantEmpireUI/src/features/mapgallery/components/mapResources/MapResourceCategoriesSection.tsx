import { useMemo, useState } from "react";

import { CodeField, EditableTable } from "@/components/ui";
import type { MapResourceDraft } from "../../tools/mapResourceDraft";

type CategoryDraft = MapResourceDraft["categories"][number];

interface MapResourceCategoriesSectionProps {
  categories: CategoryDraft[];
  typeCounts: Record<string, number>;
  onChange: (categories: CategoryDraft[]) => void;
  /** Removes a category: straight from the draft if unsaved, otherwise through a confirmed delete. */
  onRemove: (category: CategoryDraft) => void;
}

/**
 * The groups the map editor lists MapResourceTypes under (Food, Metals…). Purely for arranging the editor: nothing in play reads a category.
 * A category's code is fixed once saved, as a type's is; one with types in it cannot be removed.
 */
export default function MapResourceCategoriesSection({ categories, typeCounts, onChange, onRemove }: MapResourceCategoriesSectionProps) {
  // The name of the row whose Name field currently has focus, as it was when focus arrived. See `sortNameOf`.
  const [nameBeingEdited, setNameBeingEdited] = useState<{ index: number; name: string } | null>(null);

  const update = (index: number, patch: Partial<CategoryDraft>) => onChange(categories.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const nextSortOrder = categories.length === 0 ? 10 : Math.max(...categories.map((c) => c.sortOrder)) + 10;

  /**
   * The name a row sorts under. For every row but the one being renamed that is simply its current name; the row being renamed keeps the name it
   * had when its field gained focus, so the list holds still while it is typed in and the row takes its new place only once focus leaves.
   */
  const sortNameOf = (index: number) => (nameBeingEdited?.index === index ? nameBeingEdited.name : categories[index].displayName);

  /**
   * The positions in `categories` in the order the table shows them: by name, falling back to position so rows sharing a name keep a fixed order
   * rather than swapping about. Recomputed when the draft changes or a rename finishes, which is when a row may need to move.
   */
  const displayOrder = useMemo(
    () => [...categories.keys()].sort((a, b) => sortNameOf(a).localeCompare(sortNameOf(b)) || a - b),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sortNameOf reads exactly these two, and is redefined on every render.
    [categories, nameBeingEdited],
  );

  const orderedRows = displayOrder.map((index) => categories[index]);

  return (
    <div className="w-full max-w-2xl">
      <p className="mb-3 text-sm text-slate-500">Map Resources must belong to a Category. Define the Categories here.</p>

      <EditableTable<CategoryDraft>
        rows={orderedRows}
        addLabel="Category"
        emptyHint="No categories yet. Add one before adding Map Resources."
        // Keyed by the row's place in the draft, which a re-sort does not change, so a cell's own state (a CodeField's manually-edited flag) follows
        // its row as the row moves rather than staying behind with the position.
        rowKey={(_, displayIndex) => displayOrder[displayIndex]}
        onAddRow={() => onChange([...categories, { code: "", displayName: "New Category", sortOrder: nextSortOrder }])}
        onRemoveRow={(displayIndex) => onRemove(categories[displayOrder[displayIndex]])}
        columns={[
          {
            header: "Name",
            renderCell: (row, displayIndex) => {
              const index = displayOrder[displayIndex];

              return (
                <input
                  className="w-full rounded border border-slate-300 px-1.5 py-1 text-xs"
                  aria-label="Category name"
                  value={row.displayName}
                  onFocus={() => setNameBeingEdited({ index, name: row.displayName })}
                  onBlur={() => setNameBeingEdited(null)}
                  onChange={(e) => update(index, { displayName: e.target.value })}
                />
              );
            },
          },
          {
            header: "Code",
            renderCell: (row, displayIndex) =>
              row.id != null ? (
                <span className="text-xs text-slate-500">{row.code}</span>
              ) : (
                <CodeField
                  className="w-full rounded border border-slate-300 px-1.5 py-1 text-xs"
                  value={row.code}
                  source={row.displayName}
                  onChange={(code) => update(displayOrder[displayIndex], { code })}
                />
              ),
          },
          {
            header: "Types",
            className: "w-14 text-center",
            renderCell: (row) => <span className="text-xs text-slate-500">{typeCounts[row.code.toLowerCase()] ?? 0}</span>,
          },
        ]}
      />
    </div>
  );
}
