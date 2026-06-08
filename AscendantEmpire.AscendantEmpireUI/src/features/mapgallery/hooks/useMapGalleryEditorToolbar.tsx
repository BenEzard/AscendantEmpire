import { useMemo } from "react";
import { FaCheck, FaMountain, FaRegFolderOpen, FaTree } from "react-icons/fa6";
import { BiExport } from "react-icons/bi";
import { SlClose } from "react-icons/sl";
import { VscNewFile } from "react-icons/vsc";
import { FaPaintBrush } from "react-icons/fa";
import { HiMagnifyingGlass } from "react-icons/hi2";

import type { ToolBarGroup } from "@/components/Toolbar";
import { DropButton, HamburgerMenu, Spinner } from "@/components/ui";
import ToolbarExclusiveButtons from "@/components/ToolbarExclusiveButtons";

import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/components/OpenCampaignMapDialog";

import BrushPreview from "@/features/mapgallery/components/BrushPreview";
import type { BrushKind } from "@/features/mapgallery/tools/brush";
import { iconMap } from "@/features/mapgallery/components/mapEditorIcons";
import type { ActiveDialog, ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";

type Args = {
  biomesLoading: boolean;
  biomes: Biome[] | undefined;
  biomeMapSelection: number | null;
  setBiomeMapSelection: (v: number | null) => void;
  openBiomes: () => void;

  geographiesLoading: boolean;
  geographies: Geography[] | undefined;
  geographyMapSelection: number | null;
  setGeographyMapSelection: (v: number | null) => void;
  openGeographies: () => void;

  openMapIndex: CampaignMapGalleryIndex | null;
  closeMap: () => void;
  openExportMap: () => void;
  isExportingMap: boolean;
  setActiveDialog: (d: ActiveDialog) => void;

  activeTool: ActiveTool;
  setActiveTool: (t: ActiveTool) => void;

  brushKind: BrushKind;
  setBrushKind: (k: BrushKind) => void;
  brushRadii: Record<BrushKind, number>;
  setBrushRadii: (updater: (prev: Record<BrushKind, number>) => Record<BrushKind, number>) => void;
  clampRadius: (kind: BrushKind, value: number) => number;
};

export function useMapGalleryEditorToolbar(args: Args): ToolBarGroup[] {
  const {
    biomesLoading,
    biomes,
    biomeMapSelection,
    setBiomeMapSelection,
    openBiomes,

    geographiesLoading,
    geographies,
    geographyMapSelection,
    setGeographyMapSelection,
    openGeographies,

    openMapIndex,
    closeMap,
    openExportMap,
    isExportingMap,
    setActiveDialog,

    activeTool,
    setActiveTool,

    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    clampRadius,
  } = args;

  return useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "Map",
        buttons: [
          {
            key: "map-menu",
            render: () => (
              <HamburgerMenu
                title="Map menu"
                tooltip="Map"
                groups={[
                  {
                    label: "Map",
                    items: [
                      {
                        key: "new",
                        icon: <VscNewFile />,
                        label: "New",
                        tooltip: "New Campaign Map",
                        onClick: () => setActiveDialog("newCampaignMap"),
                      },
                      {
                        key: "open",
                        icon: <FaRegFolderOpen />,
                        label: "Open",
                        tooltip: "Open Campaign Map",
                        onClick: () => setActiveDialog("openCampaignMap"),
                      },
                      {
                        key: "close",
                        icon: <SlClose />,
                        label: "Close",
                        tooltip: "Close Campaign Map",
                        disabled: openMapIndex == null,
                        onClick: closeMap,
                      },
                    ],
                  },
                  {
                    label: "Dev Tools",
                    items: [
                      {
                        key: "push-out",
                        icon: isExportingMap ? <Spinner size="sm" /> : <BiExport />,
                        label: isExportingMap ? "Pushing Out..." : "Push Out",
                        tooltip: "Push out map changes to currently played games",
                        disabled: openMapIndex == null || isExportingMap,
                        onClick: openExportMap,
                      },
                    ],
                  },
                ]}
              />
            ),
          },
        ],
      },
      {
        caption: "Palette",
        buttons: [
          {
            key: "biomes",
            tooltip: biomesLoading ? "Loading Biomes..." : "Biomes",
            disabled: biomesLoading,
            render: ({ disabled }) => (
              <DropButton
                label="Biomes"
                title={biomesLoading ? "Loading Biomes..." : "Biomes"}
                icon={<FaTree />}
                disabled={disabled}
                onClick={openBiomes}
                menuDisabled={biomesLoading || openMapIndex == null}
                items={[
                  {
                    key: "none",
                    label: (
                      <span className="inline-flex w-full items-center gap-2">
                        <span className="inline-block h-3 w-3 border border-black/20" />
                        <span className="inline-flex items-center">
                          <span>{"<none>"}</span>
                          {biomeMapSelection == null ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
                        </span>
                      </span>
                    ),
                    active: biomeMapSelection == null,
                    onSelect: () => setBiomeMapSelection(null),
                  },
                  ...(biomes ?? [])
                    .slice()
                    .filter((b) => b.isPaintableOnCampaignMap)
                    .sort((a, b) => a.name.localeCompare(b.name))
                    .map((b) => {
                      const active = biomeMapSelection === b.biomeType;
                      const color = (b.mapEditorHtmlColor ?? "").trim();

                      return {
                        key: b.biomeType,
                        label: (
                          <span className="inline-flex w-full items-center gap-2">
                            <span className="inline-block h-3 w-3 border border-black/20" style={{ background: color || "transparent" }} />
                            <span className="inline-flex items-center">
                              <span>{b.name}</span>
                              {active ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
                            </span>
                          </span>
                        ),
                        active,
                        onSelect: () => setBiomeMapSelection(b.biomeType),
                      };
                    }),
                ]}
              />
            ),
          },
          {
            key: "geographies",
            tooltip: geographiesLoading ? "Loading Geographies..." : "Geographies",
            disabled: geographiesLoading,
            render: ({ disabled }) => (
              <DropButton
                label="Geographies"
                title={geographiesLoading ? "Loading Geographies..." : "Geographies"}
                icon={<FaMountain />}
                disabled={disabled}
                onClick={openGeographies}
                menuDisabled={geographiesLoading || openMapIndex == null}
                items={[
                  {
                    key: "none",
                    label: (
                      <span className="inline-flex w-full items-center gap-2">
                        <span className="inline-flex h-3 w-3 items-center justify-center" />
                        <span className="inline-flex items-center">
                          <span>{"<none>"}</span>
                          {geographyMapSelection == null ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
                        </span>
                      </span>
                    ),
                    active: geographyMapSelection == null,
                    onSelect: () => setGeographyMapSelection(null),
                  },
                  ...(geographies ?? [])
                    .slice()
                    .filter((b) => b.isPaintableOnCampaignMap)
                    .sort((a, b) => a.relativeElevation - b.relativeElevation)
                    .map((g) => {
                      const active = geographyMapSelection === g.campaignMapGeographyType;
                      const iconKey = (g.mapEditorIcon ?? "").trim();
                      const Icon = iconKey ? iconMap[iconKey] : undefined;

                      return {
                        key: g.campaignMapGeographyType,
                        label: (
                          <span className="inline-flex w-full items-center gap-2">
                            <span className="inline-flex h-3 w-3 items-center justify-center">
                              {Icon ? <Icon aria-hidden size={12} className="text-slate-700" /> : null}
                            </span>
                            <span className="inline-flex items-center">
                              <span>{g.name}</span>
                              {active ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
                            </span>
                          </span>
                        ),
                        active,
                        onSelect: () => setGeographyMapSelection(g.campaignMapGeographyType),
                      };
                    }),
                ]}
              />
            ),
          },
        ],
      },
      {
        caption: "Editor Tools",
        buttons: [
          {
            key: "tools",
            disabled: openMapIndex == null,
            render: ({ disabled }) => (
              <ToolbarExclusiveButtons
                disabled={disabled}
                value={activeTool}
                onChange={(k) => setActiveTool(k as ActiveTool)}
                items={[
                  { type: "button", key: "inspect", icon: <HiMagnifyingGlass />, tooltip: "Inspection" },
                  {
                    type: "dropbutton",
                    key: "paint",
                    label: "Paint",
                    icon: <FaPaintBrush />,
                    tooltip: "Paint Biome & Geography",
                    onClick: () => {
                      // main click just selects the tool; brush is chosen from the dropdown.
                    },
                    items: [
                      {
                        key: "square",
                        label: <BrushPreview kind="square" radius={clampRadius("square", brushRadii.square)} />,
                        title: (() => {
                          const r = clampRadius("square", brushRadii.square);
                          const n = 1 + r * 2;
                          return `Square ${n}×${n} (${n * n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "square",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("square");
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("square");
                          setBrushRadii((prev) => {
                            const current = prev.square ?? 0;
                            const next = clampRadius("square", current + (dir === "up" ? 1 : -1));
                            return { ...prev, square: next };
                          });
                        },
                      },
                      {
                        key: "diamond",
                        label: <BrushPreview kind="diamond" radius={clampRadius("diamond", brushRadii.diamond)} />,
                        title: (() => {
                          const r = clampRadius("diamond", brushRadii.diamond);
                          const n = 1 + r * 2;
                          return `Diamond (radius ${r}, bounds ${n}×${n}) — scroll to resize`;
                        })(),
                        active: brushKind === "diamond",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("diamond");
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("diamond");
                          setBrushRadii((prev) => {
                            const current = prev.diamond ?? 0;
                            const next = clampRadius("diamond", current + (dir === "up" ? 1 : -1));
                            return { ...prev, diamond: next };
                          });
                        },
                      },
                      {
                        key: "vline",
                        label: <BrushPreview kind="vline" radius={clampRadius("vline", brushRadii.vline)} />,
                        title: (() => {
                          const r = clampRadius("vline", brushRadii.vline);
                          const n = 1 + r * 2;
                          return `Vertical line (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "vline",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("vline");
                          setBrushRadii((prev) => ({ ...prev, vline: clampRadius("vline", prev.vline ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("vline");
                          setBrushRadii((prev) => {
                            const current = prev.vline ?? 0;
                            const next = clampRadius("vline", current + (dir === "up" ? 1 : -1));
                            return { ...prev, vline: next };
                          });
                        },
                      },
                      {
                        key: "hline",
                        label: <BrushPreview kind="hline" radius={clampRadius("hline", brushRadii.hline)} />,
                        title: (() => {
                          const r = clampRadius("hline", brushRadii.hline);
                          const n = 1 + r * 2;
                          return `Horizontal line (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "hline",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("hline");
                          setBrushRadii((prev) => ({ ...prev, hline: clampRadius("hline", prev.hline ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("hline");
                          setBrushRadii((prev) => {
                            const current = prev.hline ?? 0;
                            const next = clampRadius("hline", current + (dir === "up" ? 1 : -1));
                            return { ...prev, hline: next };
                          });
                        },
                      },
                      {
                        key: "diagDown",
                        label: <BrushPreview kind="diagDown" radius={clampRadius("diagDown", brushRadii.diagDown)} />,
                        title: (() => {
                          const r = clampRadius("diagDown", brushRadii.diagDown);
                          const n = 1 + r * 2;
                          return `Diagonal \\ (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "diagDown",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("diagDown");
                          setBrushRadii((prev) => ({ ...prev, diagDown: clampRadius("diagDown", prev.diagDown ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("diagDown");
                          setBrushRadii((prev) => {
                            const current = prev.diagDown ?? 0;
                            const next = clampRadius("diagDown", current + (dir === "up" ? 1 : -1));
                            return { ...prev, diagDown: next };
                          });
                        },
                      },
                      {
                        key: "diagUp",
                        label: <BrushPreview kind="diagUp" radius={clampRadius("diagUp", brushRadii.diagUp)} />,
                        title: (() => {
                          const r = clampRadius("diagUp", brushRadii.diagUp);
                          const n = 1 + r * 2;
                          return `Diagonal / (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "diagUp",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("diagUp");
                          setBrushRadii((prev) => ({ ...prev, diagUp: clampRadius("diagUp", prev.diagUp ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("diagUp");
                          setBrushRadii((prev) => {
                            const current = prev.diagUp ?? 0;
                            const next = clampRadius("diagUp", current + (dir === "up" ? 1 : -1));
                            return { ...prev, diagUp: next };
                          });
                        },
                      },
                    ],
                    menuDisabled: false,
                    menuLayout: "grid",
                    menuGridColumns: 3,
                  },
                ]}
              />
            ),
          },
        ],
      },
    ],
    [
      biomesLoading,
      biomes,
      biomeMapSelection,
      geographiesLoading,
      geographies,
      geographyMapSelection,
      openMapIndex,
      closeMap,
      openExportMap,
      isExportingMap,
      setActiveDialog,
      activeTool,
      setActiveTool,
      openBiomes,
      openGeographies,
      brushKind,
      setBrushKind,
      brushRadii,
      setBrushRadii,
      clampRadius,
      setBiomeMapSelection,
      setGeographyMapSelection,
    ],
  );
}
