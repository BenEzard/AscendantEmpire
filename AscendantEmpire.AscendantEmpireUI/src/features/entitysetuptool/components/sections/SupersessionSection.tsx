// SupersessionSection — the dialog's "Supersession" page. It edits the upgrade links where this
// component is the predecessor: the newer components (by code) that supersede it. Mirrors the backend
// ComponentSupersession contract, which is keyed by code and authoritative per predecessor.
//
// Controlled editor over the supersededBy array; reuses the generic EditableTable for the rows.

import EditableTable, { type EditableTableColumn } from "@/components/ui/EditableTable";

import { type ComponentSupersessionLink } from "@/features/unitdesign/types/UnitComponent";
import { inputClass } from "@/components/ui/FieldRow";

interface SupersessionSectionProps {
  supersededBy: ComponentSupersessionLink[];
  onChange: (next: ComponentSupersessionLink[]) => void;
}

export default function SupersessionSection({ supersededBy, onChange }: SupersessionSectionProps) {
  const updateLink = (index: number, next: ComponentSupersessionLink) => {
    onChange(supersededBy.map((link, i) => (i === index ? next : link)));
  };

  const addLink = () => {
    onChange([...supersededBy, { successorCode: "", isRecommended: false }]);
  };

  const removeLink = (index: number) => {
    onChange(supersededBy.filter((_, i) => i !== index));
  };

  // Marking a link recommended clears the flag on the others (at most one recommended upgrade).
  const setRecommended = (index: number, recommended: boolean) => {
    onChange(supersededBy.map((link, i) => ({ ...link, isRecommended: recommended && i === index })));
  };

  const columns: EditableTableColumn<ComponentSupersessionLink>[] = [
    {
      header: "Successor Code",
      renderCell: (link, index) => (
        <input
          className={inputClass}
          value={link.successorCode}
          placeholder="e.g. rifle_mk2"
          onChange={(e) => updateLink(index, { ...link, successorCode: e.target.value })}
        />
      ),
    },
    {
      header: "Recommended",
      className: "w-28 text-center",
      renderCell: (link, index) => (
        <input
          type="checkbox"
          checked={link.isRecommended}
          aria-label="Recommended upgrade"
          onChange={(e) => setRecommended(index, e.target.checked)}
        />
      ),
    },
  ];

  return (
    <div className="w-full max-w-xl">
      <p className="mb-2 text-xs text-slate-400">
        Newer components that supersede this one (its upgrade paths). Each code must resolve to another component when saved.
        Mark at most one as the recommended/default upgrade.
      </p>

      <EditableTable
        columns={columns}
        rows={supersededBy}
        onAddRow={addLink}
        onRemoveRow={removeLink}
        addLabel="Upgrade"
        emptyHint="No upgrade paths yet."
      />
    </div>
  );
}
