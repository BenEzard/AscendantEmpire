import ToolBar from "@/components/ui/toolbar/Toolbar";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, Button, Modal, Spinner } from "@/components/ui";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog from "./OpenCampaignMapDialog";
import ProcessProgressDialog from "@/features/processing/components/ProcessProgressDialog";
import { useCampaignMapBiomesQuery } from "@/hooks/useCampaignMapBiomes";
import BiomeListDialog from "./BiomeListDialog";
import GeographyListDialog from "./GeographyListDialog";
import MapResourceTypeManagerDialog from "./MapResourceTypeManagerDialog";
import type { CampaignMapGalleryIndex } from "../api/mapGalleryApi";
import { useMapResourceCatalogueQuery } from "../hooks/useMapResourceCatalogue";
import { useGalleryMapResourceMutations, useGalleryMapResourcesQuery } from "../hooks/useGalleryMapResources";
import MapResourcePlacementDialog from "./mapResources/MapResourcePlacementDialog";
import type { TileMapResourceMarker } from "./Tile";
import { placementsByTile, resolvePlacementClick, tileKey, typesById } from "../tools/mapResourcePlacement";
import type { GalleryMapResourceSpecifics } from "../api/galleryMapResourcesApi";
import { apiErrorMessages } from "@/util/apiErrors";
import { useCampaignMapTilesQuery, type CampaignMapTileDto } from "@/features/mapgallery/hooks/useCampaignMapTiles";
import { useCampaignMapGeographiesQuery } from "@/hooks/useCampaignMapGeographies";
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
import { usePaletteSelections } from "../hooks/usePaletteSelections";
import { useBrushTool } from "../hooks/useBrushTool";
import { useTileRendering } from "../hooks/useTileRendering";
import { useCampaignMapStartPositionsQuery, usePutCampaignMapStartPositionsMutation } from "../hooks/useCampaignMapStartPositions";
import MapPropertiesDialog from "./MapPropertiesDialog";
import { useUpdateCampaignMapRiversMutation } from "../hooks/useUpdateCampaignMapRivers";
import { currentWidthOf, toCanonicalEdge, type MapEdgeGeometry, type TileEdgeRef } from "../tools/riverEdgePicking";
import type { DrawableRiverWidth } from "@/components/map/RiverStrokeLayer";

function buildTiles2d(width: number, height: number, tiles: CampaignMapTileDto[]): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
      // A tile the server did not send has no rivers rather than unknown ones: this is the authored map, not a player's view of it.
      southRiver: "None" as const,
      eastRiver: "None" as const,
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
      southRiver: t.southRiver ?? "None",
      eastRiver: t.eastRiver ?? "None",
    };
  }

  return grid;
}

/** The open map's shape, as the river-edge helpers want it. */
function mapEdgeGeometry(index: CampaignMapGalleryIndex): MapEdgeGeometry {
  return {
    width: index.width,
    height: index.height,
    horizontalWrap: index.horizontalWrap,
    verticalWrap: index.verticalWrap,
  };
}

function MapGalleryEditor() {
  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [activeTool, setActiveTool] = useState<ActiveTool>(null);

  const queryClient = useQueryClient();
  const { openToast, toastElement } = useToast();

  // Which map is open + the in-flight generation, with tile-cache lifecycle.
  const { openMapIndex, setOpenMapIndex, generation, setGeneration, openMap, closeMap } = useMapEditorSession();

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
  const { data: mapResourceCategories, error: mapResourcesError, isLoading: mapResourcesLoading } = useMapResourceCatalogueQuery();

  // The MapResource Type Manager keeps its own draft (taken when it opens), so opening it is just showing it.
  const openMapResources = useCallback(() => setActiveDialog("mapResources"), []);

  // The MapResource chosen in the palette to place. Choosing one arms the MapResource tool; choosing "<none>" puts the editor back to inspecting.
  const [mapResourceMapSelection, setMapResourceMapSelection] = useState<number | null>(null);
  const selectMapResource = useCallback((typeId: number | null) => {
    setMapResourceMapSelection(typeId);
    setActiveTool((tool) => (typeId != null ? "mapResource" : tool === "mapResource" ? "inspect" : tool));
  }, []);

  // The width the River tool draws, and the edge under the pointer. "Medium" to start with, because it is the width a Land Force
  // can still cross and so the one an author reaches for most. There is no "erase" width: a click on an edge that already carries
  // the chosen width takes it away again.
  const [riverWidthSelection, setRiverWidthSelection] = useState<DrawableRiverWidth>("Medium");
  const [hoveredRiverEdge, setHoveredRiverEdge] = useState<TileEdgeRef | null>(null);

  // Choosing a width from the dropdown also arms the tool, the way choosing a MapResource does.
  const selectRiverWidth = useCallback((width: DrawableRiverWidth) => {
    setRiverWidthSelection(width);
    setActiveTool("river");
  }, []);

  // Dropped as soon as the tool is put away, so the marker cannot be left sitting on the map by a tool change.
  useEffect(() => {
    if (activeTool !== "river") {
      setHoveredRiverEdge(null);
    }
  }, [activeTool]);

  // Stable array reference for TileGrid so its React.memo isn't defeated each render.
  const geographiesList = useMemo(() => geographies ?? [], [geographies]);

  // ---------------------------
  // Editor tools (palette / brush / tile rendering)
  // ---------------------------
  const { biomeMapSelection, setBiomeMapSelection, geographyMapSelection, setGeographyMapSelection, openBiomes, openGeographies } = usePaletteSelections({
    biomesLoading,
    geographiesLoading,
    setActiveDialog,
  });

  const { brushKind, setBrushKind, brushRadii, setBrushRadii, clampRadius, buildBrushCoords, previewKeySet, handleTileHover, handleTileHoverEnd } =
    useBrushTool({ mapDims, activeTool, hasOpenMap: openMapIndex != null });

  const { biomeNameByType, geographyNameByType, getTileStyle, getTileTitle } = useTileRendering({ biomes, geographies, previewKeySet });

  // ---------------------------
  // Start positions (must be above onTileClick so the const is initialised first)
  // ---------------------------
  const { data: startPositionsResponse, error: startPositionsError } = useCampaignMapStartPositionsQuery(
    openMapIndex?.campaignMapGalleryIndexId ?? null,
    openMapIndex != null,
  );

  const startPositionKeys = useMemo<ReadonlySet<string>>(() => {
    const positions = startPositionsResponse?.startPositions ?? [];
    return new Set(positions.map((sp) => `${sp.xCoord},${sp.yCoord}`));
  }, [startPositionsResponse]);

  // ---------------------------
  // MapResources placed on the open map (must be above onTileClick, which reads them)
  // ---------------------------
  const openMapId = openMapIndex?.campaignMapGalleryIndexId ?? null;
  const { data: galleryMapResources, error: galleryMapResourcesError } = useGalleryMapResourcesQuery(openMapId);
  const placementMutations = useGalleryMapResourceMutations(openMapId);

  const placementsByTileKey = useMemo(() => placementsByTile(galleryMapResources), [galleryMapResources]);
  const mapResourceTypesById = useMemo(() => typesById(mapResourceCategories), [mapResourceCategories]);

  // What each tile draws. Rebuilt only when the placements or the catalogue change, so the arrays keep their identity and tiles stay memoized.
  const mapResourceMarkers = useMemo(() => {
    const markers = new Map<string, TileMapResourceMarker[]>();
    for (const [key, placements] of placementsByTileKey) {
      markers.set(
        key,
        placements.map((placement) => {
          const type = mapResourceTypesById.get(placement.mapResourceTypeId);
          return { key: placement.id, name: type?.displayName ?? `MapResource ${placement.mapResourceTypeId}`, iconPath: type?.iconPath ?? null };
        }),
      );
    }
    return markers;
  }, [placementsByTileKey, mapResourceTypesById]);

  // The placement whose dialog is open. Kept as an id, so the dialog always shows the placement as last fetched.
  const [editingPlacementId, setEditingPlacementId] = useState<number | null>(null);
  const [placementErrors, setPlacementErrors] = useState<string[]>([]);
  const editingPlacement = galleryMapResources?.find((placement) => placement.id === editingPlacementId) ?? null;

  const openPlacement = useCallback((placementId: number) => {
    setPlacementErrors([]);
    setEditingPlacementId(placementId);
  }, []);

  const closePlacement = useCallback(() => {
    setPlacementErrors([]);
    setEditingPlacementId(null);
  }, []);

  const savePlacement = useCallback(
    (specifics: GalleryMapResourceSpecifics) => {
      if (editingPlacementId == null) return;
      placementMutations.update.mutate(
        { placementId: editingPlacementId, specifics },
        { onSuccess: closePlacement, onError: (error) => setPlacementErrors(apiErrorMessages(error)) },
      );
    },
    [editingPlacementId, placementMutations.update, closePlacement],
  );

  const deletePlacement = useCallback(() => {
    if (!editingPlacement) return;
    placementMutations.remove.mutate(editingPlacement, { onSuccess: closePlacement, onError: (error) => setPlacementErrors(apiErrorMessages(error)) });
  }, [editingPlacement, placementMutations.remove, closePlacement]);

  // ---------------------------
  // Open map / tiles (above the click handlers, which read the grid to decide what a click does)
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
  // Mutations
  // ---------------------------
  const updateTilesMutation = useUpdateCampaignMapTilesMutation({
    onFailureToast: (message) => openToast(message, "danger"),
  });

  const putStartPositionsMutation = usePutCampaignMapStartPositionsMutation({
    onFailureToast: (message) => openToast(message, "danger"),
  });

  const updateRiversMutation = useUpdateCampaignMapRiversMutation({
    onFailureToast: (message) => openToast(message, "danger"),
  });

  const publishCampaignMapMutation = usePublishCampaignMapMutation({
    onSuccessToast: (message: string) => openToast(message, "success"),
    onFailureToast: (message: string) => openToast(message, "danger"),
  });

  /** What a click on the hovered edge would do, so the marker can say whether it draws or erases before the click happens. */
  const hoverWouldErase = useMemo(() => {
    if (hoveredRiverEdge == null || openMapIndex == null) {
      return false;
    }

    const canonical = toCanonicalEdge(hoveredRiverEdge, mapEdgeGeometry(openMapIndex));

    return canonical != null && currentWidthOf(tiles2d, canonical) === riverWidthSelection;
  }, [hoveredRiverEdge, openMapIndex, tiles2d, riverWidthSelection]);

  /**
   * Sets the river on the edge the pointer picked, or takes it away again.
   *
   * The tool **toggles**: an edge that already carries the chosen width is erased, and any other edge is set to it. So a
   * mis-drawn edge is undone by clicking it again, and widening a Medium river to Large is still a single click — which is why
   * there is no separate eraser to switch to and back out of.
   *
   * The edge arrives as one of the clicked tile's four sides; only South and East exist on the wire, so a north or west click is
   * translated to the neighbour that owns that edge, wrapping included. An edge facing off a map that does not wrap has no owner
   * and is refused here rather than sent, since the server would only refuse it too.
   *
   * Everything else the authoring rules check — that the river reaches the sea, and that it never widens inland — stays on the
   * server. Both need the whole river network, and a second copy of that walk in the client is a second copy to keep correct.
   */
  const onRiverEdgeClick = useCallback(
    (edge: TileEdgeRef) => {
      if (!openMapIndex) {
        openToast("No map opened.", "warning");
        return;
      }

      const canonical = toCanonicalEdge(edge, mapEdgeGeometry(openMapIndex));

      if (canonical == null) {
        openToast("That edge faces off the map.", "warning");
        return;
      }

      const alreadyThere = currentWidthOf(tiles2d, canonical) === riverWidthSelection;

      updateRiversMutation.mutate({
        campaignMapGalleryIndexId: openMapIndex.campaignMapGalleryIndexId,
        changes: [{ x: canonical.x, y: canonical.y, edge: canonical.edge, width: alreadyThere ? "None" : riverWidthSelection }],
      });
    },
    [openMapIndex, openToast, riverWidthSelection, tiles2d, updateRiversMutation],
  );

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

      if (activeTool === "mapResource") {
        if (!openMapIndex) {
          openToast("No map opened.", "warning");
          return;
        }

        // Clicking a tile that already holds the chosen MapResource opens it; otherwise it is placed, and its dialog opens for its specifics.
        const click = resolvePlacementClick(mapResourceMapSelection, placementsByTileKey.get(tileKey(t.x, t.y)));

        if (click.action === "none") {
          openToast(click.reason, "warning");
        } else if (click.action === "edit") {
          openPlacement(click.placement.id);
        } else {
          placementMutations.place.mutate(
            { xCoord: t.x, yCoord: t.y, mapResourceTypeId: mapResourceMapSelection! },
            {
              onSuccess: (placed) => openPlacement(placed.id),
              // The server says why a tile was refused (no habitat for it, or it is full).
              onError: (error) => openToast(apiErrorMessages(error).join(" "), "danger"),
            },
          );
        }
        return;
      }

      if (activeTool === "startPosition") {
        if (!openMapIndex) {
          openToast("No map opened.", "warning");
          return;
        }

        const existing = startPositionsResponse?.startPositions ?? [];
        const alreadySet = existing.some((sp) => sp.xCoord === t.x && sp.yCoord === t.y);

        // Toggle: remove if already present, add if not.
        const next = alreadySet
          ? existing
              .filter((sp) => !(sp.xCoord === t.x && sp.yCoord === t.y))
              .map((sp) => ({ xCoord: sp.xCoord, yCoord: sp.yCoord, displayName: sp.displayName, locationScore: sp.locationScore }))
          : [
              ...existing.map((sp) => ({ xCoord: sp.xCoord, yCoord: sp.yCoord, displayName: sp.displayName, locationScore: sp.locationScore })),
              { xCoord: t.x, yCoord: t.y, displayName: null, locationScore: null },
            ];

        putStartPositionsMutation.mutate({
          campaignMapGalleryIndexId: openMapIndex.campaignMapGalleryIndexId,
          startPositions: next,
        });
      }
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
      buildBrushCoords,
      startPositionsResponse,
      putStartPositionsMutation,
      mapResourceMapSelection,
      placementsByTileKey,
      openPlacement,
      placementMutations.place,
    ],
  );

  // ---------------------------
  // Push Out
  // ---------------------------
  const openPushOutMap = useCallback(() => {
    if (openMapIndex == null) return;
    setActiveDialog("pushOutCampaignMap");
  }, [openMapIndex]);

  // ---------------------------
  // Properties dialog
  // ---------------------------
  const openMapProperties = useCallback(() => {
    if (openMapIndex == null) return;
    setActiveDialog("mapProperties");
  }, [openMapIndex]);

  const confirmPushOutMap = useCallback(() => {
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

    mapResourcesLoading,
    mapResourceCategories,
    mapResourceMapSelection,
    selectMapResource,
    openMapResources,

    openMapIndex,
    closeMap,
    openPushOutMap,
    isPushingOutMap: publishCampaignMapMutation.isPending,
    setActiveDialog,
    openMapProperties,

    activeTool,
    setActiveTool,

    riverWidthSelection,
    selectRiverWidth,

    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    clampRadius,
  });

  return (
    <>
      <ToolBar
        groups={toolBarGroups}
        containerClassName="toolbar flex justify-start border-b border-slate-200 bg-stone-100 sticky top-0 z-10"
      />

      {biomeError && <Alert variant="danger">Biomes failed: {biomeError.message}</Alert>}
      {geographyError && <Alert variant="danger">Geographies failed: {geographyError.message}</Alert>}
      {mapResourcesError && <Alert variant="danger">MapResources failed: {mapResourcesError.message}</Alert>}
      {galleryMapResourcesError && <Alert variant="danger">Placed MapResources failed: {galleryMapResourcesError.message}</Alert>}
      {startPositionsError && <Alert variant="danger">Start positions failed: {startPositionsError.message}</Alert>}

      {/* ====== Opened map display ====== */}
      <div className="mt-3">
        {openMapIndex == null ? (
          <Alert variant="secondary" className="mb-0">
            No map opened. Use <strong>File → Open</strong>.
          </Alert>
        ) : (
          <>
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
                startPositionKeys={startPositionKeys}
                mapResourceMarkers={mapResourceMarkers}
                // Only wired while the River tool is on: the picker sits over the grid and takes the clicks, which is what the tool
                // wants and what every other tool must not have happen to it.
                onRiverEdgeClick={activeTool === "river" ? onRiverEdgeClick : undefined}
                onRiverEdgeHover={activeTool === "river" ? setHoveredRiverEdge : undefined}
                hoveredRiverEdge={hoveredRiverEdge}
                hoveredRiverEdgeErases={hoverWouldErase}
              />
            )}
          </>
        )}
      </div>

      {/* Read-only: both catalogues are authored in the Entity Setup Tool, so the editor only looks them up. */}
      <BiomeListDialog show={activeDialog === "biomes"} biomes={biomes ?? []} onClose={() => setActiveDialog(null)} />

      <GeographyListDialog show={activeDialog === "geographies"} geographies={geographies ?? []} onClose={() => setActiveDialog(null)} />

      {(() => {
        // The edited placement's tile, for the habitat its starting quantity would be rolled from.
        const tile = editingPlacement ? (tiles2d?.[editingPlacement.yCoord]?.[editingPlacement.xCoord] ?? null) : null;
        const biomeName = tile?.biomeTypeId != null ? biomeNameByType.get(tile.biomeTypeId) : undefined;
        const geographyName = tile?.geographyTypeId != null ? geographyNameByType.get(tile.geographyTypeId) : undefined;

        return (
          <MapResourcePlacementDialog
            placement={editingPlacement}
            type={editingPlacement ? mapResourceTypesById.get(editingPlacement.mapResourceTypeId) : undefined}
            biomeType={tile?.biomeTypeId ?? null}
            geographyType={tile?.geographyTypeId ?? null}
            tileDescription={[geographyName, biomeName].filter(Boolean).join(" ") || "unknown terrain"}
            busy={placementMutations.update.isPending || placementMutations.remove.isPending}
            errors={placementErrors}
            onSave={savePlacement}
            onDelete={deletePlacement}
            onClose={closePlacement}
          />
        );
      })()}

      <MapResourceTypeManagerDialog
        show={activeDialog === "mapResources"}
        biomes={biomes ?? []}
        geographies={geographiesList}
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

      <MapPropertiesDialog
        show={activeDialog === "mapProperties"}
        mapIndex={openMapIndex}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(updatedName, updatedDescription) => {
          // Keep the in-memory openMapIndex in sync so the editor reflects the new name immediately.
          if (openMapIndex != null) {
            setOpenMapIndex({ ...openMapIndex, mapName: updatedName, description: updatedDescription ?? "" });
          }
          openToast("Map properties saved.", "success");
        }}
        onFailureToast={(message) => openToast(message, "danger")}
      />

      <Modal
        open={activeDialog === "pushOutCampaignMap"}
        onClose={() => {
          if (publishCampaignMapMutation.isPending) return;
          setActiveDialog(null);
        }}
        title="Pushing Out Campaign Map"
        footer={
          <>
            <Button variant="secondary" onClick={() => setActiveDialog(null)} disabled={publishCampaignMapMutation.isPending}>
              No
            </Button>
            <Button variant="danger" onClick={confirmPushOutMap} disabled={publishCampaignMapMutation.isPending || openMapIndex == null}>
              {publishCampaignMapMutation.isPending ? "Pushing Out..." : "Yes"}
            </Button>
          </>
        }>
        <p>Pushing out map updates can be destructive to ongoing games. Are you sure you want to do this?</p>
      </Modal>

      {(tilesLoading || tilesFetching) && (
        <div style={{ position: "fixed", bottom: "1rem", right: "1rem", zIndex: 50 }}>
          <Alert variant="secondary" className="flex items-center gap-2 mb-0 py-2 px-3">
            <Spinner size="sm" />
            <span>Loading tiles…</span>
          </Alert>
        </div>
      )}

      {toastElement}
    </>
  );
}

export default MapGalleryEditor;
