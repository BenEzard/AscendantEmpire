import ToolBar from "@/components/ui/toolbar/Toolbar";
import ResourceManagerDialog from "@/features/game/resources/manager/ResourceManagerDialog";
import { usePlayerCampaignMapTilesQuery } from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid, { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { CampaignMapTileDto } from "@/features/map/api/playerCampaignMapApi";
import type { TileCell } from "@/features/map/components/TileTypes";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import { useUnitDisplayInfoQuery } from "@/features/map/hooks/useUnitDisplayInfo";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Alert, Spinner } from "@/components/ui";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";
import MilitaryAdvisorDialog from "@/features/game/advisors/military/MilitaryAdvisorDialog";
import OpenGameDialog from "./OpenGameDialog";
import VisionSettingsOffcanvas from "./VisionSettingsOffcanvas";
import { http } from "@/api/http";
import { enterGame } from "@/features/auth/api/authApi";
import ContextActionToolbar from "./ContextActionToolbar";
import ForceSelectionToolbar from "./ForceSelectionToolbar";
import NotificationsHost from "@/features/notifications/NotificationsHost";
import { NotificationsProvider } from "@/features/notifications/useNotifications";
import NewGameDialog from "./NewGameDialog";
import { useToast } from "@/hooks/useToast";
import { useElementBottomPx } from "@/hooks/useElementBottomPx";
import { useGameSession } from "../hooks/useGameSession";
import { useVisionTool } from "../hooks/useVisionTool";
import { useForceSelection } from "../hooks/useForceSelection";
import { useTileRendering } from "../hooks/useTileRendering";
import { useConstructionPanel } from "../hooks/useConstructionPanel";
import { useGameToolbar } from "./useGameToolbar";
import ConstructionPanel from "./ConstructionPanel";

type ActivePanel = "unit-design" | null;

function buildTiles2d(width: number, height: number, tiles: CampaignMapTileDto[]): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
      structures: undefined,
      forces: undefined,
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
      structures: t.structures,
      forces: t.forces,
    };
  }

  return grid;
}

function GameUI({ topOffset = 0 }: { topOffset?: number }) {
  const [activePanel, setActivePanel] = useState<ActivePanel>(null);
  const [notificationPanelOpen, setNotificationPanelOpen] = useState(false);
  const [showMilitaryAdvisor, setShowMilitaryAdvisor] = useState(false);

  const toolbarRef = useRef<HTMLDivElement | null>(null);
  const tileGridContainerRef = useRef<HTMLDivElement | null>(null);
  const toolbarBottomPx = useElementBottomPx(toolbarRef);

  const { openToast, toastElement } = useToast();

  // --- Game session / dialogs ---
  const { gameSelection, setGameSelection, gameId, playerId, activeDialog, setActiveDialog, closeGame } = useGameSession();
  const tilesEnabled = gameId != null && playerId != null;

  // --- Tools ---
  const {
    activeTool: visionActiveTool,
    selectedTile,
    setSelectedTile,
    observedTileKeys,
    visionRadius,
    setVisionRadius,
    visionLoadWholeMap,
    setVisionLoadWholeMap,
    showVisionSettings,
    setShowVisionSettings,
    toggleVisionTool,
    openVisionSettings,
    clearVision,
    sendVisionRequest,
  } = useVisionTool({ gameId, playerId, openToast });

  const {
    selectedForces,
    selectedForceIds,
    multiForceTile,
    getVisibleForceId,
    chooseForce,
    onTileHold,
    selectForceFromTile,
    contextActionToolbarActions,
  } = useForceSelection({ gameId, playerId, openToast });

  const construction = useConstructionPanel();

  // --- Map data ---
  const { data: biomes } = useCampaignMapBiomesQuery();
  const { data: geographies } = useCampaignMapGeographiesQuery();

  // Stable array reference for TileGrid so its React.memo isn't defeated each render.
  const geographiesList = useMemo(() => geographies ?? [], [geographies]);

  const {
    data: tilesResponse,
    error: tilesError,
    isLoading: tilesLoading,
    isFetching: tilesFetching,
  } = usePlayerCampaignMapTilesQuery(gameId, playerId, tilesEnabled);
  const { data: unitDisplayLookup } = useUnitDisplayInfoQuery(gameId, gameId != null);
  const unitImageSrcByUnitTemplateId = unitDisplayLookup?.imageSrcByUnitTemplateId;
  const unitNameByUnitTemplateId = unitDisplayLookup?.nameByUnitTemplateId;

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;
    return buildTiles2d(gameSelection.width, gameSelection.height, tilesResponse.tiles);
  }, [gameSelection, tilesResponse]);

  const { biomeNameByType, geographyNameByType, getTileStyle, getTileTitle } = useTileRendering({ biomes, geographies, selectedTile, observedTileKeys });

  // --- Map panning (used by the notifications panel) ---
  const panMapTo = useCallback((x: number, y: number) => {
    const el = tileGridContainerRef.current;
    if (!el) return;

    const tileCenterX = x * TILE_SIZE_PX + TILE_SIZE_PX / 2;
    const tileCenterY = y * TILE_SIZE_PX + TILE_SIZE_PX / 2;

    const hasInternalHScroll = el.scrollWidth > el.clientWidth;
    const hasInternalVScroll = el.scrollHeight > el.clientHeight;

    if (hasInternalHScroll || hasInternalVScroll) {
      // Container is height/width-constrained and scrolls internally.
      const maxLeft = Math.max(0, el.scrollWidth - el.clientWidth);
      const maxTop = Math.max(0, el.scrollHeight - el.clientHeight);
      el.scrollTo({
        left: Math.max(0, Math.min(maxLeft, tileCenterX - el.clientWidth / 2)),
        top: Math.max(0, Math.min(maxTop, tileCenterY - el.clientHeight / 2)),
        behavior: "smooth",
      });
    } else {
      // Container expands to its content (no internal scroll) — the window scrolls instead.
      const rect = el.getBoundingClientRect();
      const absLeft = rect.left + window.scrollX + tileCenterX;
      const absTop = rect.top + window.scrollY + tileCenterY;
      window.scrollTo({
        left: Math.max(0, absLeft - window.innerWidth / 2),
        top: Math.max(0, absTop - window.innerHeight / 2),
        behavior: "smooth",
      });
    }
  }, []);

  const openNotificationDialog = useCallback(
    (dialog: "unit-orders" | "trade" | "city") => {
      openToast(`Open dialog: ${dialog}`, "info");
    },
    [openToast],
  );

  // --- Tile click: vision + force selection + inspect toast ---
  const onTileClick = useCallback(
    (t: TileCell) => {
      if (visionActiveTool === "vision") {
        setSelectedTile({ x: t.x, y: t.y });
        sendVisionRequest(t.x, t.y);
      }

      selectForceFromTile(t);

      const biomeName = t.biomeTypeId == null ? "null" : (biomeNameByType.get(t.biomeTypeId) ?? `unknown(${t.biomeTypeId})`);
      const geoName = t.geographyTypeId == null ? "null" : (geographyNameByType.get(t.geographyTypeId) ?? `unknown(${t.geographyTypeId})`);
      const label = biomeName === "Unknown" && geoName === "Unknown" ? "Unknown" : `${biomeName}, ${geoName}`;

      openToast(`[${t.x}, ${t.y}]: ${label}`, "warning");
    },
    [visionActiveTool, setSelectedTile, sendVisionRequest, selectForceFromTile, biomeNameByType, geographyNameByType, openToast],
  );

  // --- Toolbar wiring ---
  const openNewGame = useCallback(() => setActiveDialog("newGame"), [setActiveDialog]);
  const openOpenGame = useCallback(() => setActiveDialog("openGame"), [setActiveDialog]);
  const toggleUnitDesign = useCallback(() => setActivePanel((p) => (p === "unit-design" ? null : "unit-design")), []);
  const toggleMilitaryAdvisor = useCallback(() => setShowMilitaryAdvisor((shown) => !shown), []);
  const closeMilitaryAdvisor = useCallback(() => setShowMilitaryAdvisor(false), []);

  // Dismiss the Military Advisor when the game closes — its toolbar button is disabled without an
  // open game, so an already-open dialog would otherwise be left showing stale, unreopenable state.
  // Watching `gameSelection` rather than hooking `closeGame` covers any future path that clears it.
  useEffect(() => {
    if (gameSelection == null) {
      setShowMilitaryAdvisor(false);
    }
  }, [gameSelection]);

  const onEndTurn = useCallback(async () => {
    if (gameId == null || playerId == null) return;

    // No payload: the server ends the turn of whoever the token says the caller is.
    try {
      await http.post("/turn/end-campaign");
      // optional: only toggle after success
      setActivePanel((p) => (p === "unit-design" ? null : "unit-design"));
    } catch (err) {
      console.error("POST /turn/end-campaign failed", err);
    }
  }, [gameId, playerId]);

  const toolBarGroups = useGameToolbar({
    gameOpen: gameSelection != null,
    onNewGame: openNewGame,
    onOpenGame: openOpenGame,
    onCloseGame: closeGame,
    onClearVision: clearVision,
    visionActive: visionActiveTool === "vision",
    onToggleVision: toggleVisionTool,
    onOpenVisionSettings: openVisionSettings,
    onToggleConstruction: construction.toggleConstructionPanel,
    unitDesignActive: activePanel === "unit-design",
    onToggleUnitDesign: toggleUnitDesign,
    onEndTurn,
    militaryAdvisorActive: showMilitaryAdvisor,
    onToggleMilitaryAdvisor: toggleMilitaryAdvisor,
  });

  return (
    <NotificationsProvider>
      <div className="flex flex-col" style={{ minHeight: "100vh", paddingRight: notificationPanelOpen ? "20vw" : undefined }}>
        <div ref={toolbarRef} className="fixed left-0 right-0 z-[1070]" style={{ top: topOffset }}>
          <ToolBar groups={toolBarGroups} />
        </div>
        {/* Spacer so content doesn't slide under the fixed toolbar (toolbar height only, tab strip is handled by App) */}
        <div style={{ height: Math.max(0, toolbarBottomPx - topOffset) }} />

        {gameSelection != null && (
          <NotificationsHost
            toolbarBottomPx={toolbarBottomPx}
            onPanTo={panMapTo}
            onOpenDialog={openNotificationDialog}
            onPanelOpenChange={setNotificationPanelOpen}
          />
        )}

        {construction.showConstructionPanel && (
          <ConstructionPanel
            overlayRef={construction.overlayRef}
            top={construction.overlayTop}
            left={construction.overlayLeft}
            categories={construction.constructionCategories}
            setCategories={construction.setConstructionCategories}
            onClickResource={(code) => {
              construction.setResourceManagerSelectedId(code);
              construction.setShowResourceManager(true);
            }}
          />
        )}

        <VisionSettingsOffcanvas
          show={showVisionSettings}
          onHide={() => setShowVisionSettings(false)}
          visionRadius={visionRadius}
          setVisionRadius={setVisionRadius}
          visionLoadWholeMap={visionLoadWholeMap}
          setVisionLoadWholeMap={setVisionLoadWholeMap}
        />

        <NewGameDialog
          show={activeDialog === "newGame"}
          onClose={() => setActiveDialog(null)}
          onCreateSuccess={(msg) => openToast(msg ?? "Game created", "success")}
        />

        <OpenGameDialog
          show={activeDialog === "openGame"}
          onClose={() => setActiveDialog(null)}
          onSaveSuccess={(msg) => openToast(msg ?? "Undefined", "success")}
          onOpenMap={async (selection) => {
            setActiveDialog(null);
            try {
              // Swap the user token for a game token before loading the game, so protected
              // endpoints read this game + player straight from the token.
              await enterGame(selection.gameId);
              setGameSelection(selection);
            } catch (err) {
              openToast(err instanceof Error ? err.message : "Could not enter that game.", "danger");
            }
          }}
        />

        <ResourceManagerDialog
          show={construction.showResourceManager}
          onClose={() => construction.setShowResourceManager(false)}
          tickers={construction.allConstructionTickers}
          selectedResourceId={construction.resourceManagerSelectedId}
          onSelectResource={construction.setResourceManagerSelectedId}
          onCommit={(changes, tradePartners) => {
            console.log("[GameUI] ResourceManager changes committed:", changes);
            console.log("[GameUI] ResourceManager trade partners committed:", tradePartners);
          }}
        />

        <MilitaryAdvisorDialog show={showMilitaryAdvisor} onClose={closeMilitaryAdvisor} />

        {/* Both bars stack in one fixed column, so the selection bar sits under the action toolbar without either
            needing to know the other's height.

            Mounted while anything is selected *or* the bar has a tile to offer. The second half matters: ctrl+
            clicking the last selected force empties the selection, and if the dock unmounted on that the bar would
            vanish out from under the cursor that was clicking in it, leaving the player to go back to the map to
            recover it. With a tile still set, the bar stays and the action toolbar simply empties. */}
        {(selectedForces.length > 0 || multiForceTile != null) && (
          <div className="command-dock">
            <ContextActionToolbar visible={contextActionToolbarActions.length > 0} actions={contextActionToolbarActions} />
            <ForceSelectionToolbar
              visible={multiForceTile != null}
              tile={multiForceTile}
              selectedForceIds={selectedForceIds}
              unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
              unitNameByUnitTemplateId={unitNameByUnitTemplateId}
              onSelect={chooseForce}
            />
          </div>
        )}

        {toastElement}

        <div className="flex flex-col grow min-h-0">
          {activePanel === "unit-design" ? (
            <UnitDesigner
              onClose={() => setActivePanel(null)}
              onSaveSuccess={(msg) => openToast(msg, "success")}
              onSaveError={(msg) => openToast(msg, "danger")}
            />
          ) : (
            <div className="mt-3">
              {gameSelection == null ? (
                <Alert variant="secondary" className="mb-0">
                  No game opened. Use <strong>Game → Open</strong>.
                </Alert>
              ) : (
                <>
                  <div className="flex items-center justify-between mb-2">
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
                      width={gameSelection.width}
                      height={gameSelection.height}
                      tiles2d={tiles2d}
                      geographies={geographiesList}
                      containerRef={tileGridContainerRef}
                      selectedForceIds={selectedForceIds}
                      unitImageSrcByUnitTemplateId={unitImageSrcByUnitTemplateId}
                      getVisibleForceId={getVisibleForceId}
                      onTileClick={onTileClick}
                      onTileHold={onTileHold}
                      getTileStyle={getTileStyle}
                      getTileTitle={getTileTitle}
                    />
                  )}
                </>
              )}
            </div>
          )}
        </div>
      </div>
    </NotificationsProvider>
  );
}

export default GameUI;
