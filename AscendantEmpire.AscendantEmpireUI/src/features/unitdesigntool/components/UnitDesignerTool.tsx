// Stub for the developer-only "Unit Designer" tool, created alongside the landing-page split.
// This is a standalone dev tool like the Map Editor (no AuthGate) and is intentionally separate
// from the game-side `unitdesign` feature. The real functionality will be filled in later.
import { useMemo, useState } from "react";
import { FaPlus } from "react-icons/fa6";
import { HiMagnifyingGlass } from "react-icons/hi2";

import ToolBar from "@/components/Toolbar";
import type { ToolBarGroup } from "@/components/Toolbar";

import { createTag } from "../api/tagsApi";
import type { Tag } from "../types/Tag";
import ComponentSetupDialog from "./ComponentSetupDialog";
import TagSetupDialog from "./TagSetupDialog";

export default function UnitDesignerTool() {
  // Controls the ComponentSetupDialog opened by the toolbar's "Add" button.
  const [showComponentSetup, setShowComponentSetup] = useState(false);

  // Controls the TagSetupDialog opened by the Tags group's "Add Tag" button.
  const [showTagSetup, setShowTagSetup] = useState(false);

  // Persists a newly authored tag, then closes the dialog. Errors are logged for now — this dev tool
  // has no toast wiring yet; surfacing them in the UI can come with the Search/edit flow.
  const handleTagCommit = async (tag: Tag) => {
    try {
      await createTag({
        code: tag.code,
        displayName: tag.displayName,
        description: tag.description,
        tagType: tag.tagType,
      });
      setShowTagSetup(false);
    } catch (error) {
      console.error("Failed to create tag.", error);
    }
  };

  // "Components" group plus a "Tags" group. Each group's Add opens its setup dialog; the search
  // buttons remain placeholders until their browsers are built.
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
      {
        caption: "Tags",
        buttons: [
          {
            key: "add-tag",
            icon: <FaPlus />,
            tooltip: "Add Tag",
            onClick: () => setShowTagSetup(true),
          },
          {
            key: "search-tag",
            icon: <HiMagnifyingGlass />,
            tooltip: "Search Tag",
            onClick: () => {
              // TODO: open the tag search (stub for now).
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

      <TagSetupDialog open={showTagSetup} onCancel={() => setShowTagSetup(false)} onCommit={handleTagCommit} />
    </div>
  );
}
