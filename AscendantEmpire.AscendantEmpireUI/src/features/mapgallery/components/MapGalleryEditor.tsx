import ToolBar from "@/components/Toolbar";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { Biome } from "../../../types/map/Biome";
import { Alert, Button, Modal, Spinner, Toast, ToastViewport } from "@/components/ui";
import type { Geography } from "../../../types/map/Geography";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog, { type CampaignMapGalleryIndex } from "./OpenCampaignMapDialog";
import { useCampaignMapBiomesQuery } from "../hooks/useCampaignMapBiomes";
import BiomeManagerDialog from "./BiomeManagerDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";
import { mapGalleryTilesKey, useCampaignMapTilesQuery } from "@/features/mapgallery/hooks/useCampaignMapTiles";
import { useCampaignMapGeographiesQuery } from "../hooks/useCampaignMapGeographies";
import type { TileCell } from "../types/TileTypes";
import TileGrid from "./TileGrid";
import { onInspectTileClick } from "../tools/inspectTool";
import { useQueryClient } from "@tanstack/react-query";
import { useUpdateCampaignMapTilesMutation } from "../hooks/useUpdateCampaignMapTiles";
import { pickTextColorOnBg } from "@/util/color";
import { buildBrushCoordinates, clampBrushRadius, type BrushKind } from "@/features/mapgallery/tools/brush";
import type { ActiveDialog, ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";
import { useMapGalleryEditorToolbar } from "@/features/mapgallery/hooks/useMapGalleryEditorToolbar";
import { getFirstBiomeType, getFirstGeographyType } from "@/features/mapgallery/tools/paletteDraft";
import { usePublishCampaignMapMutation } from "../hooks/usePublishCampaignMap";

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

  const mapDims = useMemo(
    () =>
      openMapIndex
        ? {
            width: Math.max(0, openMapIndex.width),
            height: Math.max(0, openMapIndex.height),
          }
        : null,
    [openMapIndex],
  );

  const clampRadius = useCallback((kind: BrushKind, value: number) => clampBrushRadius(kind, value, mapDims), [mapDims]);

  useEffect(() => {
    // If the user opens a smaller map, ensure stored radii are still valid.
    setBrushRadii((prev) => {
      const next: Record<BrushKind, number> = { ...prev };
      (Object.keys(next) as BrushKind[]).forEach((k) => {
        next[k] = clampRadius(k, next[k] ?? 0);
      });
      return next;
    });
  }, [clampRadius]);

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

  const publishCampaignMapMutation = usePublishCampaignMapMutation({
    onSuccessToast: (message: string) => openToast(message, "success"),
    onFailureToast: (message: string) => openToast(message, "danger"),
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

  const buildBrushCoords = useCallback(
    (origin: TileCell) =>
      buildBrushCoordinates({
        origin,
        kind: brushKind,
        radius: brushRadii[brushKind] ?? 0,
        dims: mapDims,
      }),
    [brushKind, brushRadii, mapDims],
  );

  const previewKeySet = useMemo(() => {
    if (activeTool !== "paint") return null;
    if (!openMapIndex) return null;
    if (!hoveredTile) return null;

    const coords = buildBrushCoords(hoveredTile);
    return new Set(coords.map((c) => `${c.x},${c.y}`));
  }, [activeTool, openMapIndex, hoveredTile, buildBrushCoords]);

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

        const coordinates = buildBrushCoords(t);

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

  const openExportMap = useCallback(() => {
    if (openMapIndex == null) return;
    setActiveDialog("exportCampaignMap");
  }, [openMapIndex]);

  const confirmExportMap = useCallback(() => {
    if (openMapIndex == null || publishCampaignMapMutation.isPending) return;

    publishCampaignMapMutation.mutate(
      {
        campaignMapGalleryIndexId: openMapIndex.campaignMapGalleryIndexId,
        mapName: openMapIndex.mapName,
      },
      {
        onSuccess: () => {
          setActiveDialog(null);
        },
      },
    );
  }, [openMapIndex, publishCampaignMapMutation]);

  const toolBarGroups = useMapGalleryEditorToolbar({
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
    isExportingMap: publishCampaignMapMutation.isPending,
    setActiveDialog,

    activeTool,
    setActiveTool,

    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    clampRadius,
  });

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

      <Modal
        open={activeDialog === "exportCampaignMap"}
        onClose={() => {
          if (publishCampaignMapMutation.isPending) return;
          setActiveDialog(null);
        }}
        title="Export Map"
        footer={
          <>
            <Button variant="secondary" onClick={() => setActiveDialog(null)} disabled={publishCampaignMapMutation.isPending}>
              No
            </Button>
            <Button variant="danger" onClick={confirmExportMap} disabled={publishCampaignMapMutation.isPending || openMapIndex == null}>
              {publishCampaignMapMutation.isPending ? "Exporting..." : "Yes"}
            </Button>
          </>
        }>
        <p>Exporting map updates can be destructive to ongoing games. Are you sure you want to do this?</p>
      </Modal>

      <ToastViewport>
        <Toast open={toast.show} onClose={closeToast} variant={toast.variant} message={toast.message} autoHideMs={3000} />
      </ToastViewport>
    </>
  );
}

export default MapGalleryEditor;
