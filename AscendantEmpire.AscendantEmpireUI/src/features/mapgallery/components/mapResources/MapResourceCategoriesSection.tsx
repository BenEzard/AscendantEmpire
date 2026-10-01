import { CodeField, EditableTable } from "@/components/ui";
import type { MapResourceDraft } from "../../tools/mapResourceDraft";
import { WholeNumberInput } from "./mapResourceFields";

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
  const update = (index: number, patch: Partial<CategoryDraft>) => onChange(categories.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const nextSortOrder = categories.length === 0 ? 10 : Math.max(...categories.map((c) => c.sortOrder)) + 10;

  return (
    <div className="w-full max-w-2xl">
      <EditableTable<CategoryDraft>
        rows={categories}
        addLabel="Category"
        emptyHint="No categories yet. Add one before adding Map Resources."
        onAddRow={() => onChange([...categories, { code: "", displayName: "New Category", sortOrder: nextSortOrder }])}
        onRemoveRow={(index) => onRemove(categories[index])}
        columns={[
          {
            header: "Name",
            renderCell: (row, index) => (
              <input
                className="w-full rounded border border-slate-300 px-1.5 py-1 text-xs"
                aria-label="Category name"
                value={row.displayName}
                onChange={(e) => update(index, { displayName: e.target.value })}
              />
            ),
          },
          {
            header: "Code",
            renderCell: (row, index) =>
              row.id != null ? (
                <span className="text-xs text-slate-500">{row.code}</span>
              ) : (
                <CodeField
                  className="w-full rounded border border-slate-300 px-1.5 py-1 text-xs"
                  value={row.code}
                  source={row.displayName}
                  onChange={(code) => update(index, { code })}
                />
              ),
          },
          {
            header: "Order",
            className: "w-20",
            renderCell: (row, index) => (
              <WholeNumberInput compact required ariaLabel="Sort order" value={row.sortOrder} onChange={(value) => update(index, { sortOrder: value })} />
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
