// Stub for the developer-only "Unit Designer" tool, created alongside the landing-page split.
// This is a standalone dev tool like the Map Editor (no AuthGate) and is intentionally separate
// from the game-side `unitdesign` feature. The real functionality will be filled in later.
import { useMemo, useState } from "react";
import { FaPlus } from "react-icons/fa6";
import { HiMagnifyingGlass } from "react-icons/hi2";

import ToolBar from "@/components/Toolbar";
import type { ToolBarGroup } from "@/components/Toolbar";

import ComponentSetupDialog from "./ComponentSetupDialog";

export default function UnitDesignerTool() {
  // Controls the ComponentSetupDialog opened by the toolbar's "Add" button.
  const [showComponentSetup, setShowComponentSetup] = useState(false);

  // Single "Components" group for now. Add opens the component-setup dialog; search
  // remains a placeholder until the component browser is built.
  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "Components",
        buttons: [
          {
            key: "add-component",
            icon: <FaPlus />,
            tooltip: "Add Component",
            onClick: () => setShowComponentSetup(true),
          },
          {
            key: "search-components",
            icon: <HiMagnifyingGlass />,
            tooltip: "Search Components",
            onClick: () => {
              // TODO: open the component search.
            },
          },
        ],
      },
    ],
    [],
  );

  return (
    <div className="flex min-h-screen flex-col bg-slate-900 text-slate-200">
      <ToolBar
        groups={toolBarGroups}
        containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10"
      />

      <div className="flex flex-1 flex-col items-center justify-center gap-2">
        <h1 className="text-2xl font-semibold">Unit Designer</h1>
        <p className="text-sm text-slate-400">Developer tool — stub. Functionality coming soon.</p>
      </div>

      <ComponentSetupDialog
        open={showComponentSetup}
        onCancel={() => setShowComponentSetup(false)}
        onCommit={() => {
          // TODO: persist the component (BulkUpsertUnitComponents) once the tool is wired to the API.
          setShowComponentSetup(false);
        }}
      />
    </div>
  );
}
