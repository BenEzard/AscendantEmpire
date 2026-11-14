import type { ReactNode } from "react";

import { Button, PagedDialog, dialogTitle } from "@/components/ui";

/**
 * The height the content region is pinned to.
 *
 * <remarks>
 * Left as a fixed number rather than sized to the tallest page, so the dialog never resizes as the player pages through it —
 * see `PagedDialogProps.height`. Pages that outgrow it scroll inside it.
 * </remarks>
 */
const CONTENT_HEIGHT_PX = 520;

export interface SettlementManagerDialogProps {
  show: boolean;
  /** The settlement being managed, which is also what identifies it: settlement names are unique in the world. Null while none is open. */
  settlementName: string | null;
  onClose: () => void;
}

/** A page with nothing on it yet, so the shell reads as unfinished rather than broken. */
function Placeholder({ children }: { children: ReactNode }) {
  return <div className="flex h-full items-center justify-center text-sm text-slate-400">{children}</div>;
}

/**
 * Manages one settlement.
 *
 * <remarks>
 * <para>
 * Opened by double-clicking a Campaign Map tile one of the player's own settlements stands on. A shell for now: every page is a placeholder, because
 * nothing reads a settlement yet — there is no settlement endpoint, and the map read carries only its name and completion.
 * </para>
 * <para>
 * Large and pinned, both deliberately. The panel takes Modal's widest preset, and the content region is fixed at
 * `CONTENT_HEIGHT_PX` so the dialog holds its size from page to page.
 * </para>
 * </remarks>
 */
export default function SettlementManagerDialog({ show, settlementName, onClose }: SettlementManagerDialogProps) {
  return (
    <PagedDialog
      open={show}
      title={dialogTitle("Settlement Manager", settlementName)}
      size="xl"
      height={CONTENT_HEIGHT_PX}
      panelClassName="thick-border-modal"
      onCancel={onClose}
      footer={
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      }
      sections={[
        { key: "overview", label: "Overview", content: <Placeholder>Nothing to show here yet.</Placeholder> },
        { key: "population", label: "Population", content: <Placeholder>Nothing to show here yet.</Placeholder> },
        { key: "production", label: "Production", content: <Placeholder>Nothing to show here yet.</Placeholder> },
        { key: "buildings", label: "Buildings", content: <Placeholder>Nothing to show here yet.</Placeholder> },
      ]}
    />
  );
}
