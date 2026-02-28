import { FaCheck, FaMountain, FaRegFolderOpen, FaTree } from "react-icons/fa6";
import { SlClose } from "react-icons/sl";
import ToolBar, { type ToolBarGroup } from "@/components/Toolbar";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { Biome } from "../../../types/map/Biome";
import { Alert, Spinner, Toast, ToastViewport } from "@/components/ui";
import type { Geography } from "../../../types/map/Geography";
import { VscNewFile } from "react-icons/vsc";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog, { type CampaignMapGalleryIndex } from "./OpenCampaignMapDialog";
import { useCampaignMapBiomesQuery } from "../hooks/useCampaignMapBiomes";
import BiomeManagerDialog from "./BiomeManagerDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";
import { mapGalleryTilesKey, useCampaignMapTilesQuery } from "@/features/mapgallery/hooks/useCampaignMapTiles";
import { useCampaignMapGeographiesQuery } from "../hooks/useCampaignMapGeographies";
import type { TileCell } from "../types/TileTypes";
import TileGrid from "./TileGrid";
import { DropButton } from "@/components/ui";
import { FaPaintBrush } from "react-icons/fa";
import { HiMagnifyingGlass } from "react-icons/hi2";
import ToolbarExclusiveButtons from "@/components/ToolbarExclusiveButtons";
import { onInspectTileClick } from "../tools/inspectTool";
import { useQueryClient } from "@tanstack/react-query";
import { useUpdateCampaignMapTilesMutation } from "../hooks/useUpdateCampaignMapTiles";
import { iconMap } from "./mapEditorIcons";
import { pickTextColorOnBg } from "@/util/color";

type BrushKind = "square" | "diamond" | "vline" | "hline" | "diagDown" | "diagUp";

const MAX_BRUSH_RADIUS = 5;

const BRUSH_CONSTRAINTS: Record<
  BrushKind,
  {
    // radius 0 => size 1, radius 1 => size 3, etc.
    minRadius: number;
    maxRadiusByDims: (dims: { width: number; height: number }) => number;
  }
> = {
  square: {
    // Single-cell brush: minimum size is 1.
    minRadius: 0,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  diamond: {
    // All other brushes: minimum size is 3.
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  vline: {
    minRadius: 1,
    maxRadiusByDims: ({ height }) => Math.floor((height - 1) / 2),
  },
  hline: {
    minRadius: 1,
    maxRadiusByDims: ({ width }) => Math.floor((width - 1) / 2),
  },
  diagDown: {
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  diagUp: {
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
};

function buildTiles2d(
  width: number,
  height: number,
  tiles: { xCoord: number; yCoord: number; biomeTypeId: number | null; campaignMapGeographyTypeId: number | null }[],
): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
    })),
  );

  for (const t of tiles) {
    if (t.xCoord < 0 || t.xCoord >= width) continue;
    if (t.yCoord < 0 || t.yCoord >= height) continue;

    grid[t.yCoord][t.xCoord] = {
      x: t.xCoord,
      y: t.yCoord,
      biomeTypeId: t.biomeTypeId,
      geographyTypeId: t.campaignMapGeographyTypeId,
    };
  }

  return grid;
}

function MapGalleryEditor() {
  type ActiveDialog = "biomes" | "geographies" | "newCampaignMap" | "openCampaignMap" | null;
  type ActiveTool = "paint" | "inspect" | null;

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [activeTool, setActiveTool] = useState<ActiveTool>(null);
  const [brushKind, setBrushKind] = useState<BrushKind>("square");
  const [brushRadii, setBrushRadii] = useState<Record<BrushKind, number>>({
    square: 0,
    diamond: 1,
    vline: 1,
    hline: 1,
    diagDown: 1,
    diagUp: 1,
  });
  const [hoveredTile, setHoveredTile] = useState<TileCell | null>(null);

  const queryClient = useQueryClient();

  // --- Biomes (draft owned by parent) ---
  const [biomeDraft, setBiomeDraft] = useState<Biome[]>([]);
  const [biomeMapSelection, setBiomeMapSelection] = useState<number | null>(null);
  const [biomeManagerSelection, setBiomeManagerSelection] = useState<number | null>(null);

  // --- Geographies (draft owned by parent) ---
  const [geographyDraft, setGeographyDraft] = useState<Geography[]>([]);
  const [geographyMapSelection, setGeographyMapSelection] = useState<number | null>(null);
  const [geographyEditorSelection, setGeographyEditorSelection] = useState<number | null>(null);

  // "Open map" selection
  const [openMapIndex, setOpenMapIndex] = useState<CampaignMapGalleryIndex | null>(null);

  // Tools are only available when a map is open.
  useEffect(() => {
    if (openMapIndex == null) {
      setActiveTool(null);
      setHoveredTile(null);
      return;
    }

    // Default tool when enabling tools.
    setActiveTool((t) => t ?? "inspect");
  }, [openMapIndex]);

  const getMaxRadiusForBrush = useCallback(
    (kind: BrushKind) => {
      if (!openMapIndex) return MAX_BRUSH_RADIUS;

      const dims = {
        width: Math.max(0, openMapIndex.width),
        height: Math.max(0, openMapIndex.height),
      };

      const maxByDims = BRUSH_CONSTRAINTS[kind].maxRadiusByDims(dims);

      return Math.max(0, Math.min(MAX_BRUSH_RADIUS, maxByDims));
    },
    [openMapIndex],
  );

  const clampBrushRadius = useCallback(
    (kind: BrushKind, value: number) => {
      const max = getMaxRadiusForBrush(kind);

      const minRequested = BRUSH_CONSTRAINTS[kind].minRadius;
      const min = Math.min(minRequested, max);

      return Math.max(min, Math.min(max, value));
    },
    [getMaxRadiusForBrush],
  );

  useEffect(() => {
    // If the user opens a smaller map, ensure stored radii are still valid.
    setBrushRadii((prev) => {
      const next: Record<BrushKind, number> = { ...prev };
      (Object.keys(next) as BrushKind[]).forEach((k) => {
        next[k] = clampBrushRadius(k, next[k] ?? 0);
      });
      return next;
    });
  }, [clampBrushRadius]);

  useEffect(() => {
    if (activeTool !== "paint") setHoveredTile(null);
  }, [activeTool]);

  // ---------------------------
  // Toast
  // ---------------------------
  const [toast, setToast] = useState<{
    show: boolean;
    message: string;
    variant: "success" | "danger" | "warning" | "info";
  }>({
    show: false,
    message: "",
    variant: "success",
  });

  const openToast = useCallback((message: string, variant: "success" | "danger" | "warning" | "info" = "success") => {
    setToast({ show: true, message, variant });
  }, []);

  const closeToast = () => setToast((t) => ({ ...t, show: false }));

  const updateTilesMutation = useUpdateCampaignMapTilesMutation({
    onFailureToast: (message) => openToast(message, "danger"),
  });

  const closeMap = useCallback(() => {
    if (openMapIndex) {
      const key = mapGalleryTilesKey(openMapIndex.campaignMapGalleryIndexId);
      queryClient.cancelQueries({ queryKey: key });
      queryClient.removeQueries({ queryKey: key });
    }

    setOpenMapIndex(null);
  }, [openMapIndex, queryClient]);

  // ---------------------------
  // Queries
  // ---------------------------
  const { data: biomes, error: biomeError, isLoading: biomesLoading } = useCampaignMapBiomesQuery();

  const { data: geographies, error: geographyError, isLoading: geographiesLoading } = useCampaignMapGeographiesQuery();

  // ---------------------------
  // Lookups for tile view
  // ---------------------------
  const biomeColorByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) {
      const c = (b.mapEditorHtmlColor ?? "").trim();
      if (c) m.set(b.biomeType, c);
    }
    return m;
  }, [biomes]);

  const biomeNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) m.set(b.biomeType, b.name);
    return m;
  }, [biomes]);

  const geographyNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const g of geographies ?? []) {
      // assuming your Geography id is campaignMapGeographyType (as in your dialog)
      m.set(g.campaignMapGeographyType, g.name);
    }
    return m;
  }, [geographies]);

  const buildBrushCoordinates = useCallback(
    (origin: TileCell) => {
      if (!openMapIndex) return [] as { x: number; y: number }[];

      const inBounds = (x: number, y: number) => x >= 0 && y >= 0 && x < openMapIndex.width && y < openMapIndex.height;

      const addUnique = (set: Set<string>, coords: { x: number; y: number }[], x: number, y: number) => {
        if (!inBounds(x, y)) return;
        const k = `${x},${y}`;
        if (set.has(k)) return;
        set.add(k);
        coords.push({ x, y });
      };

      const coordinates: { x: number; y: number }[] = [];
      const seen = new Set<string>();

      const r = clampBrushRadius(brushKind, brushRadii[brushKind] ?? 0);

      if (brushKind === "square") {
        for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) addUnique(seen, coordinates, origin.x + dx, origin.y + dy);
      } else if (brushKind === "diamond") {
        for (let dy = -r; dy <= r; dy++) {
          for (let dx = -r; dx <= r; dx++) {
            if (Math.abs(dx) + Math.abs(dy) <= r) addUnique(seen, coordinates, origin.x + dx, origin.y + dy);
          }
        }
      } else if (brushKind === "vline") {
        for (let dy = -r; dy <= r; dy++) addUnique(seen, coordinates, origin.x, origin.y + dy);
      } else if (brushKind === "hline") {
        for (let dx = -r; dx <= r; dx++) addUnique(seen, coordinates, origin.x + dx, origin.y);
      } else if (brushKind === "diagDown") {
        for (let d = -r; d <= r; d++) addUnique(seen, coordinates, origin.x + d, origin.y + d);
      } else if (brushKind === "diagUp") {
        for (let d = -r; d <= r; d++) addUnique(seen, coordinates, origin.x + d, origin.y - d);
      }

      return coordinates;
    },
    [brushKind, brushRadii, openMapIndex, clampBrushRadius],
  );

  const previewKeySet = useMemo(() => {
    if (activeTool !== "paint") return null;
    if (!openMapIndex) return null;
    if (!hoveredTile) return null;

    const coords = buildBrushCoordinates(hoveredTile);
    return new Set(coords.map((c) => `${c.x},${c.y}`));
  }, [activeTool, openMapIndex, hoveredTile, buildBrushCoordinates]);

  const onTileClick = useCallback(
    (t: TileCell) => {
      if (activeTool === "inspect") {
        onInspectTileClick(t, { openToast, biomeNameByType, geographyNameByType });
        return;
      }

      if (activeTool === "paint") {
        if (!openMapIndex) {
          openToast("No map opened.", "warning");
          return;
        }

        // If both are "<none>", there's nothing to change.
        if (biomeMapSelection == null && geographyMapSelection == null) {
          openToast("Select a biome or geography to paint (or both).", "warning");
          return;
        }

        const coordinates = buildBrushCoordinates(t);

        updateTilesMutation.mutate({
          campaignMapGalleryIndexId: openMapIndex.campaignMapGalleryIndexId,
          coordinates,
          biomeType: biomeMapSelection,
          geographyType: geographyMapSelection,
        });

        return;
      }

      // Other tools (e.g. paint) can implement their own click behavior.
    },
    [
      activeTool,
      openToast,
      biomeNameByType,
      geographyNameByType,
      openMapIndex,
      biomeMapSelection,
      geographyMapSelection,
      updateTilesMutation,
      buildBrushCoordinates,
    ],
  );

  const renderBrushPreview = useCallback(
    (kind: BrushKind, radius: number) => {
      const Square = ({ filled }: { filled: boolean }) => (
        <span className={["inline-block h-1 w-1 border border-black/20", filled ? "bg-black/50" : "bg-transparent"].join(" ")} />
      );

      const r = clampBrushRadius(kind, radius);
      const n = 1 + r * 2;

      if (kind === "vline") {
        return (
          <span className="grid" style={{ gridTemplateColumns: "repeat(1, 1fr)", gap: 1 }}>
            {Array.from({ length: n }).map((_, i) => (
              <Square key={i} filled />
            ))}
          </span>
        );
      }

      if (kind === "hline") {
        return (
          <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
            {Array.from({ length: n }).map((_, i) => (
              <Square key={i} filled />
            ))}
          </span>
        );
      }

      if (kind === "diagDown" || kind === "diagUp") {
        return (
          <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
            {Array.from({ length: n * n }).map((_, idx) => {
              const x = (idx % n) - r;
              const y = Math.floor(idx / n) - r;
              const filled = kind === "diagDown" ? y === x : y === -x;
              return <Square key={idx} filled={filled} />;
            })}
          </span>
        );
      }

      if (kind === "square") {
        return (
          <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
            {Array.from({ length: n * n }).map((_, i) => (
              <Square key={i} filled />
            ))}
          </span>
        );
      }

      if (kind === "diamond") {
        return (
          <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
            {Array.from({ length: n * n }).map((_, idx) => {
              const x = (idx % n) - r;
              const y = Math.floor(idx / n) - r;
              const filled = Math.abs(x) + Math.abs(y) <= r;
              return <Square key={idx} filled={filled} />;
            })}
          </span>
        );
      }

      return null;
    },
    [clampBrushRadius],
  );

  // ---------------------------
  // Open map / tiles
  // ---------------------------
  const {
    data: tilesResponse,
    error: tilesError,
    isLoading: tilesLoading,
    isFetching: tilesFetching,
  } = useCampaignMapTilesQuery(openMapIndex?.campaignMapGalleryIndexId ?? null, openMapIndex != null);

  const tiles2d = useMemo(() => {
    if (!openMapIndex || !tilesResponse) return null;
    return buildTiles2d(openMapIndex.width, openMapIndex.height, tilesResponse.tiles);
  }, [openMapIndex, tilesResponse]);

  // ---------------------------
  // Biomes: draft helpers
  // ---------------------------
  const getFirstBiomeType = (list: Biome[]) => list.slice().sort((a, b) => a.name.localeCompare(b.name))[0]?.biomeType ?? null;

  const BIOME_DEFAULTS = {
    regenerationRate: 1.1,
    movementCost: 1,
    isTraversable: true,
    isWater: false,
    isPaintableOnCampaignMap: true,
    doesBlockSight: false,
  };

  const createNewBiome = (nextId: number): Biome => ({
    biomeType: nextId,
    name: "new",
    isTraversable: BIOME_DEFAULTS.isTraversable,
    movementCost: BIOME_DEFAULTS.movementCost,
    isWater: BIOME_DEFAULTS.isWater,
    doesBlockSight: BIOME_DEFAULTS.doesBlockSight,
    canBeElevated: false,
    isPaintableOnCampaignMap: BIOME_DEFAULTS.isPaintableOnCampaignMap,
    mapEditorHtmlColor: "",
    regenerationRate: BIOME_DEFAULTS.regenerationRate,
    baseForageValue: null,
    baseHuntValue: null,
    baseFishValue: null,
    baseFodderValue: null,
    baseBoneValue: null,
    baseWoodValue: null,
    baseStoneValue: null,
    baseSpecialValue: null,
  });

  const addNewBiomeAndOpen = () => {
    const baseList = activeDialog === "biomes" ? biomeDraft : (biomes ?? []);
    const nextId = Math.max(0, ...baseList.map((b) => b.biomeType)) + 1;

    const newBiome = createNewBiome(nextId);

    setBiomeDraft([...baseList, newBiome]);
    setBiomeManagerSelection(newBiome.biomeType);
    setActiveDialog("biomes");
  };

  // ---------------------------
  // Geographies: draft helpers (mirrors Biomes)
  // ---------------------------
  const getFirstGeographyType = (list: Geography[]) => list.slice().sort((a, b) => a.name.localeCompare(b.name))[0]?.campaignMapGeographyType ?? null;

  const GEO_DEFAULTS = {
    relativeElevation: 0,
    movementCost: 1,
    isPaintableOnCampaignMap: true,
  };

  const createNewGeography = (nextId: number): Geography => ({
    campaignMapGeographyType: nextId,
    name: "new",
    relativeElevation: GEO_DEFAULTS.relativeElevation,
    movementCost: GEO_DEFAULTS.movementCost,
    mapEditorIcon: null,
    isPaintableOnCampaignMap: GEO_DEFAULTS.isPaintableOnCampaignMap,
  });

  const addNewGeographyAndOpen = () => {
    const baseList = activeDialog === "geographies" ? geographyDraft : (geographies ?? []);
    const nextId = Math.max(0, ...baseList.map((g) => g.campaignMapGeographyType)) + 1;

    const newGeo = createNewGeography(nextId);

    setGeographyDraft([...baseList, newGeo]);
    setGeographyEditorSelection(newGeo.campaignMapGeographyType);
    setActiveDialog("geographies");
  };

  const getTileStyle = useCallback(
    (t: TileCell): React.CSSProperties => {
      const bg = t.biomeTypeId != null ? biomeColorByType.get(t.biomeTypeId) : undefined;
      const preview = previewKeySet?.has(`${t.x},${t.y}`) ?? false;
      const previewStroke = bg ? pickTextColorOnBg(bg) : "#000000";

      return {
        background: bg ?? "rgba(0,0,0,0.03)",
        boxShadow: preview ? `inset 0 0 0 2px ${previewStroke}` : undefined,
      };
    },
    [biomeColorByType, previewKeySet],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";

      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";

      return `(${t.x}, ${t.y}); ${biomeName}, ${geographyName}`;
    },
    [biomeNameByType, geographyNameByType],
  );

  // ---------------------------
  // ToolBar
  // ---------------------------
  const openBiomes = useCallback(() => {
    if (biomesLoading) return;

    const list = biomes ?? [];

    setBiomeDraft(list);

    setBiomeManagerSelection(getFirstBiomeType(list));

    setActiveDialog("biomes");
  }, [biomesLoading, biomes]);

  const openGeographies = useCallback(() => {
    if (geographiesLoading) return;

    const list = geographies ?? [];

    setGeographyDraft(list);

    setGeographyEditorSelection(getFirstGeographyType(list));

    setActiveDialog("geographies");
  }, [geographiesLoading, geographies]);

  const middleAddBiome = useCallback(() => {
    if (biomesLoading) return;
    addNewBiomeAndOpen();
  }, [biomesLoading, addNewBiomeAndOpen]);

  const middleAddGeography = useCallback(() => {
    if (geographiesLoading) return;
    addNewGeographyAndOpen();
  }, [geographiesLoading, addNewGeographyAndOpen]);

  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "File",
        buttons: [
          { key: "new", icon: <VscNewFile />, tooltip: "New Campaign Map", onClick: () => setActiveDialog("newCampaignMap") },
          {
            key: "open",
            icon: <FaRegFolderOpen />,
            tooltip: "Open Campaign Map",
            onClick: () => setActiveDialog("openCampaignMap"),
          },
          {
            key: "close",
            icon: <SlClose />,
            tooltip: "Close Campaign Map",
            disabled: openMapIndex == null,
            onClick: closeMap,
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
                menuDisabled={biomesLoading}
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
            onMiddleClick: middleAddBiome,
            middleClickHint: "add new",
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
                menuDisabled={geographiesLoading}
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
            onMiddleClick: middleAddGeography,
            middleClickHint: "add new",
          },
        ],
      },
      {
        caption: "Tools",
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
                        label: renderBrushPreview("square", clampBrushRadius("square", brushRadii.square)),
                        title: (() => {
                          const r = clampBrushRadius("square", brushRadii.square);
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
                            const next = clampBrushRadius("square", current + (dir === "up" ? 1 : -1));
                            return { ...prev, square: next };
                          });
                        },
                      },
                      {
                        key: "diamond",
                        label: renderBrushPreview("diamond", clampBrushRadius("diamond", brushRadii.diamond)),
                        title: (() => {
                          const r = clampBrushRadius("diamond", brushRadii.diamond);
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
                            const next = clampBrushRadius("diamond", current + (dir === "up" ? 1 : -1));
                            return { ...prev, diamond: next };
                          });
                        },
                      },
                      {
                        key: "vline",
                        label: renderBrushPreview("vline", clampBrushRadius("vline", brushRadii.vline)),
                        title: (() => {
                          const r = clampBrushRadius("vline", brushRadii.vline);
                          const n = 1 + r * 2;
                          return `Vertical line (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "vline",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("vline");
                          setBrushRadii((prev) => ({ ...prev, vline: clampBrushRadius("vline", prev.vline ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("vline");
                          setBrushRadii((prev) => {
                            const current = prev.vline ?? 0;
                            const next = clampBrushRadius("vline", current + (dir === "up" ? 1 : -1));
                            return { ...prev, vline: next };
                          });
                        },
                      },
                      {
                        key: "hline",
                        label: renderBrushPreview("hline", clampBrushRadius("hline", brushRadii.hline)),
                        title: (() => {
                          const r = clampBrushRadius("hline", brushRadii.hline);
                          const n = 1 + r * 2;
                          return `Horizontal line (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "hline",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("hline");
                          setBrushRadii((prev) => ({ ...prev, hline: clampBrushRadius("hline", prev.hline ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("hline");
                          setBrushRadii((prev) => {
                            const current = prev.hline ?? 0;
                            const next = clampBrushRadius("hline", current + (dir === "up" ? 1 : -1));
                            return { ...prev, hline: next };
                          });
                        },
                      },
                      {
                        key: "diagDown",
                        label: renderBrushPreview("diagDown", clampBrushRadius("diagDown", brushRadii.diagDown)),
                        title: (() => {
                          const r = clampBrushRadius("diagDown", brushRadii.diagDown);
                          const n = 1 + r * 2;
                          return `Diagonal \\ (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "diagDown",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("diagDown");
                          setBrushRadii((prev) => ({ ...prev, diagDown: clampBrushRadius("diagDown", prev.diagDown ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("diagDown");
                          setBrushRadii((prev) => {
                            const current = prev.diagDown ?? 0;
                            const next = clampBrushRadius("diagDown", current + (dir === "up" ? 1 : -1));
                            return { ...prev, diagDown: next };
                          });
                        },
                      },
                      {
                        key: "diagUp",
                        label: renderBrushPreview("diagUp", clampBrushRadius("diagUp", brushRadii.diagUp)),
                        title: (() => {
                          const r = clampBrushRadius("diagUp", brushRadii.diagUp);
                          const n = 1 + r * 2;
                          return `Diagonal / (${n} tiles) — scroll to resize`;
                        })(),
                        active: brushKind === "diagUp",
                        onSelect: () => {
                          setActiveTool("paint");
                          setBrushKind("diagUp");
                          setBrushRadii((prev) => ({ ...prev, diagUp: clampBrushRadius("diagUp", prev.diagUp ?? 0) }));
                        },
                        onWheel: (dir) => {
                          setActiveTool("paint");
                          setBrushKind("diagUp");
                          setBrushRadii((prev) => {
                            const current = prev.diagUp ?? 0;
                            const next = clampBrushRadius("diagUp", current + (dir === "up" ? 1 : -1));
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
      activeTool,
      openBiomes,
      middleAddBiome,
      openGeographies,
      middleAddGeography,
      brushKind,
      brushRadii,
      renderBrushPreview,
    ],
  );

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      {biomeError && <Alert variant="danger">Biomes failed: {biomeError.message}</Alert>}
      {geographyError && <Alert variant="danger">Geographies failed: {geographyError.message}</Alert>}

      {/* ====== Opened map display ====== */}
      <div className="mt-3">
        {openMapIndex == null ? (
          <Alert variant="secondary" className="mb-0">
            No map opened. Use <strong>File → Open</strong>.
          </Alert>
        ) : (
          <>
            <div className="flex items-center justify-between mb-2">
              <div>
                <strong>{openMapIndex.mapName}</strong>{" "}
                <span className="text-slate-500">
                  ({openMapIndex.width}×{openMapIndex.height})
                </span>
                <div className="text-slate-500" style={{ fontSize: 12 }}>
                  {openMapIndex.description}
                </div>
              </div>

              {(tilesLoading || tilesFetching) && (
                <div className="flex items-center gap-2 text-slate-500">
                  <Spinner size="sm" />
                  <span>Loading tiles...</span>
                </div>
              )}
            </div>

            {tilesError && <Alert variant="danger">Failed to load tiles: {tilesError.message}</Alert>}

            {tiles2d && (
              <TileGrid
                width={openMapIndex.width}
                height={openMapIndex.height}
                tiles2d={tiles2d}
                geographies={geographies ?? []}
                onTileClick={onTileClick}
                onTileHover={activeTool === "paint" ? (t) => setHoveredTile(t) : undefined}
                onTileHoverEnd={activeTool === "paint" ? () => setHoveredTile(null) : undefined}
                getTileStyle={getTileStyle}
                getTileTitle={getTileTitle}
              />
            )}
          </>
        )}
      </div>

      <BiomeManagerDialog
        show={activeDialog === "biomes"}
        biomes={biomeDraft}
        biomeManagerSelection={biomeManagerSelection}
        onSelectBiomeManagerSelection={setBiomeManagerSelection}
        setBiomes={setBiomeDraft}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <GeographyManagerDialog
        show={activeDialog === "geographies"}
        geographies={geographyDraft}
        geographyEditorSelection={geographyEditorSelection}
        onSelectGeographyEditorSelection={setGeographyEditorSelection}
        setGeographies={setGeographyDraft}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <NewCampaignMapDialog
        show={activeDialog === "newCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <OpenCampaignMapDialog
        show={activeDialog === "openCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
        onOpenMap={(index) => {
          // User made a selection; ensure tiles reload fresh even if re-opening the same map.
          queryClient.invalidateQueries({ queryKey: mapGalleryTilesKey(index.campaignMapGalleryIndexId) });
          setOpenMapIndex(index);
          setActiveDialog(null);
        }}
      />

      <ToastViewport>
        <Toast open={toast.show} onClose={closeToast} variant={toast.variant} message={toast.message} autoHideMs={3000} />
      </ToastViewport>
    </>
  );
}

export default MapGalleryEditor;
