import { useEffect, useState } from "react";
import { Modal, Button, Alert } from "@/components/ui";

/**
 * The catalogue kinds the export dialog can offer. This list is intentionally data-driven so new kinds
 * can be added with a single entry; the ids match the flags the export API expects.
 */
interface ExportSelectionItem {
  id: string;
  label: string;
}

const EXPORT_SELECTION_ITEMS: ExportSelectionItem[] = [
  { id: "templates", label: "Unit Templates" },
  { id: "components", label: "Unit Components" },
];

export interface ExportSelectionDialogProps {
  open: boolean;
  onCancel: () => void;
  /** Called with the ids of the ticked kinds when the user confirms the export. */
  onCommit: (selectedIds: string[]) => void;
  error?: string | null;
}

/**
 * A checkbox list letting the developer choose which catalogue kinds to export. Selection resets each time
 * the dialog opens so a previous choice never leaks into the next export.
 */
export default function ExportSelectionDialog({
  open,
  onCancel,
  onCommit,
  error,
}: ExportSelectionDialogProps) {
  const [selected, setSelected] = useState<Set<string>>(new Set());

  // Re-seed to empty on every open.
  useEffect(() => {
    if (open) {
      setSelected(new Set());
    }
  }, [open]);

  const handleToggle = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  return (
    <Modal
      open={open}
      onClose={onCancel}
      title="Export Catalogue"
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="success" disabled={selected.size === 0} onClick={() => onCommit([...selected])}>
            Export
          </Button>
        </>
      }>
      {error && (
        <Alert variant="danger" className="mb-3">
          {error}
        </Alert>
      )}

      <p className="mb-2 text-sm text-slate-600">Choose which catalogue data to include in the export.</p>

      <div className="space-y-1">
        {EXPORT_SELECTION_ITEMS.map((item) => (
          <label
            key={item.id}
            className="flex items-center gap-2 rounded px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
            <input type="checkbox" checked={selected.has(item.id)} onChange={() => handleToggle(item.id)} />
            {item.label}
          </label>
        ))}
      </div>
    </Modal>
  );
}
