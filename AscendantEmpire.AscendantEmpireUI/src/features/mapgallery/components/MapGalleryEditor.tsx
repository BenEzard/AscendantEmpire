import ToolBar from "@/components/Toolbar";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Button, Modal, Spinner } from "@/components/ui";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog from "./OpenCampaignMapDialog";
import ProcessProgressDialog from "@/features/processing/components/ProcessProgressDialog";
import { useCampaignMapBiomesQuery } from "../hooks/useCampaignMapBiomes";
import BiomeManagerDialog from "./BiomeManagerDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";
import { useCampaignMapTilesQuery } from "@/features/mapgallery/hooks/useCampaignMapTiles";
import { useCampaignMapGeographiesQuery } from "../hooks/useCampaignMapGeographies";
import type { TileCell } from "../types/TileTypes";
import TileGrid from "./TileGrid";
import { onInspectTileClick } from "../tools/inspectTool";
import { useQueryClient } from "@tanstack/react-query";
import { useUpdateCampaignMapTilesMutation } from "../hooks/useUpdateCampaignMapTiles";
import type { ActiveDialog, ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";
import { useMapGalleryEditorToolbar } from "@/features/mapgallery/hooks/useMapGalleryEditorToolbar";
import { usePublishCampaignMapMutation } from "../hooks/usePublishCampaignMap";
import { useToast } from "@/hooks/useToast";
import { useMapEditorSession } from "../hooks/useMapEditorSession";
import { usePaletteDrafts } from "../hooks/usePaletteDrafts";
import { useBrushTool } from "../hooks/useBrushTool";
import { useTileRendering } from "../hooks/useTileRendering";

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

  const queryClient = useQueryClient();
  const { openToast, toastElement } = useToast();

  // Which map is open + the in-flight generation, with tile-cache lifecycle.
  const { openMapIndex, generation, setGeneration, openMap, closeMap } = useMapEditorSession();

  // Tools are only available when a map is open.
  useEffect(() => {
    if (openMapIndex == null) {
      setActiveTool(null);
      return;
    }

    // Default tool when enabling tools.
    setActiveTool((t) => t ?? "inspect");
  }, [openMapIndex]);

  const mapDims = useMemo(
    () => (openMapIndex ? { width: Math.max(0, openMapIndex.width), height: Math.max(0, openMapIndex.height) } : null),
    [openMapIndex],
  );

  // ---------------------------
  // Queries
  // ---------------------------
  const { data: biomes, error: biomeError, isLoading: biomesLoading } = useCampaignMapBiomesQuery();
  const { data: geographies, error: geographyError, isLoading: geographiesLoading } = useCampaignMapGeographiesQuery();

  // Stable array reference for TileGrid so its React.memo isn't defeated each render.
  const geographiesList = useMemo(() => geographies ?? [], [geographies]);

  // ---------------------------
  // Editor tools (palette / brush / tile rendering)
  // ---------------------------
  const {
    biomeDraft,
    setBiomeDraft,
    biomeMapSelection,
    setBiomeMapSelection,
    biomeManagerSelection,
    setBiomeManagerSelection,
    geographyDraft,
    setGeographyDraft,
    geographyMapSelection,
    setGeographyMapSelection,
    geographyEditorSelection,
    setGeographyEditorSelection,
    openBiomes,
    openGeographies,
  } = usePaletteDrafts({ biomes, biomesLoading, geographies, geographiesLoading, setActiveDialog });

  const { brushKind, setBrushKind, brushRadii, setBrushRadii, clampRadius, buildBrushCoords, previewKeySet, handleTileHover, handleTileHoverEnd } =
    useBrushTool({ mapDims, activeTool, hasOpenMap: openMapIndex != null });

  const { biomeNameByType, geographyNameByType, getTileStyle, getTileTitle } = useTileRendering({ biomes, geographies, previewKeySet });

  // ---------------------------
  // Mutations
  // ---------------------------
  const updateTilesMutation = useUpdateCampaignMapTilesMutation({
    onFailureToast: (message) => openToast(message, "danger"),
  });

  const publishCampaignMapMutation = usePublishCampaignMapMutation({
    onSuccessToast: (message: string) => openToast(message, "success"),
    onFailureToast: (message: string) => openToast(message, "danger"),
  });

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
      }
    },
    [activeTool, openToast, biomeNameByType, geographyNameByType, openMapIndex, biomeMapSelection, geographyMapSelection, updateTilesMutation, buildBrushCoords],
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
  // Export ("push out")
  // ---------------------------
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

  // ---------------------------
  // ToolBar
  // ---------------------------
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
                geographies={geographiesList}
                onTileClick={onTileClick}
                onTileHover={activeTool === "paint" ? handleTileHover : undefined}
                onTileHoverEnd={activeTool === "paint" ? handleTileHoverEnd : undefined}
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
        onSaveSuccess={({ processingId, mapName }) => {
          // Open the progress dialog and start polling generation status.
          setGeneration({ processingId, mapName });
        }}
      />

      <ProcessProgressDialog
        open={generation !== null}
        title={generation ? `Generating Campaign Map "${generation.mapName}"` : "Generating map"}
        activeLabel="Generating map…"
        successMessage="Map generated successfully."
        processId={generation?.processingId ?? null}
        onComplete={() => {
          // Refresh the gallery list so the newly generated map appears.
          queryClient.invalidateQueries({ queryKey: ["mapGalleryIndexes"] });
        }}
        onClose={() => setGeneration(null)}
      />

      <OpenCampaignMapDialog
        show={activeDialog === "openCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
        onOpenMap={(index) => {
          openMap(index);
          setActiveDialog(null);
        }}
      />

      <Modal
        open={activeDialog === "exportCampaignMap"}
        onClose={() => {
          if (publishCampaignMapMutation.isPending) return;
          setActiveDialog(null);
        }}
        title="Exporting Campaign Map"
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

      {toastElement}
    </>
  );
}

export default MapGalleryEditor;
