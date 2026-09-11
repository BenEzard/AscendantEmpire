import ToolBar from "@/components/ui/toolbar/Toolbar";
import ResourceManagerDialog from "@/features/game/resources/manager/ResourceManagerDialog";
import { usePlayerCampaignMapTilesQuery } from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid, { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { TileCell } from "@/features/map/components/TileTypes";
import { buildTiles2d, type TileCellSourceMap } from "@/features/map/util/buildTiles2d";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import { useUnitDisplayInfoQuery } from "@/features/map/hooks/useUnitDisplayInfo";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Alert, Spinner } from "@/components/ui";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";
import MilitaryAdvisorDialog from "@/features/game/advisors/military/MilitaryAdvisorDialog";
import OpenGameDialog from "./OpenGameDialog";
import VisionSettingsOffcanvas from "./VisionSettingsOffcanvas";
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
import { useFogOfWar } from "../hooks/useFogOfWar";
import { useForceSelection } from "../hooks/useForceSelection";
import { useForceStepMutation } from "../hooks/useForceStep";
import { useEndTurnMutation } from "../hooks/useEndTurn";
// Remove in F-18. Temporary developer hot-seat switching.
import { useDevEndTurnSwitch } from "../hooks/useDevEndTurnSwitch";
import { useNumpadMovement } from "../hooks/useNumpadMovement";
import type { MoveDirection } from "../api/forceMovementApi";
import type { PossibleAction } from "../api/turnApi";
import { useTileRendering } from "../hooks/useTileRendering";
import { useConstructionPanel } from "../hooks/useConstructionPanel";
import { useGameToolbar } from "./useGameToolbar";
import ConstructionPanel from "./ConstructionPanel";
import { useKnownForcesQuery, useSightingReportsQuery } from "@/features/intelligence/hooks/useIntelligenceQueries";
import { groupKnownForcesByUnobservedTile } from "@/features/intelligence/knownForceMarkers";
import type { SightingReport } from "@/features/intelligence/api/intelligenceApi";
import type { SightingDescriptionContext } from "@/features/intelligence/describeSightingReport";
import { useCurrentCampaignTurnQuery } from "../hooks/useCurrentCampaignTurn";
import { useGamePlayersQuery } from "../hooks/useGamePlayers";

type ActivePanel = "unit-design" | null;

// One shared empty feed, so the notifications provider is not handed a fresh array — and made to rebuild its groups — on every render before the feed loads.
const NO_SIGHTING_REPORTS: SightingReport[] = [];


function GameUI({ topOffset = 0 }: { topOffset?: number }) {
  const [activePanel, setActivePanel] = useState<ActivePanel>(null);
  const [notificationPanelOpen, setNotificationPanelOpen] = useState(false);
  const [showMilitaryAdvisor, setShowMilitaryAdvisor] = useState(false);

  // What the server reported as still outstanding when the player last asked to end their turn. This is what the notifications panel is built from,
  // and the turn has not ended while it holds anything — nothing was written by the check that produced it.
  const [endTurnActions, setEndTurnActions] = useState<PossibleAction[]>([]);

  // Set once the player has been shown what they have outstanding, so the next press of End Turn ends the turn regardless. Disarmed again whenever
  // the turn ends or the player moves a force, because either makes the list they were shown out of date.
  const [endTurnArmed, setEndTurnArmed] = useState(false);

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
    selectForceFromTile,
    contextActionToolbarActions,
    getForceToStep,
    getStepToTile,
    onForceMoved,
  } = useForceSelection({ gameId, playerId, openToast });

  // --- Movement (numpad, and hold-to-move on the map) ---
  const stepForce = useForceStepMutation(gameId, playerId, {
    onSuccess: (result) => {
      // The whole result goes to the selection: it carries the destination tile and everything standing on it, which is what
      // lets the selection bar re-point itself at where the force now is rather than where it set off from.
      //
      // The fog needs nothing here: the mutation has already written the step's tiles and observers into the tiles cache, and
      // both fog layers are derived from that cache.
      onForceMoved(result);

      // The player has just changed what they have outstanding, so the list they were shown no longer stands and the turn should not end on the
      // strength of it. The notifications stay up — they are still the checklist being worked through — but End Turn goes back to checking first.
      setEndTurnArmed(false);
    },
    // The API writes its refusals to be read as they stand — which tile, and why it can't be entered — so they go straight to
    // the player rather than being reworded here.
    onError: (err) => openToast(err instanceof Error ? err.message : "That force could not move.", "danger"),
  });

  const onNumpadStep = useCallback(
    (direction: MoveDirection) => {
      const force = getForceToStep();
      if (force == null) return;

      stepForce.mutate({ unitInstanceId: force.id, direction });
    },
    [getForceToStep, stepForce],
  );

  const onSingleForceOnly = useCallback(
    () => openToast("Forces move one at a time for now. Select a single force to move it.", "warning"),
    [openToast],
  );

  useNumpadMovement({
    enabled: tilesEnabled,
    selectedForceCount: selectedForces.length,
    onStep: onNumpadStep,
    onUnsupportedSelection: onSingleForceOnly,
  });

  /**
   * Holding a tile orders the selected force onto it.
   *
   * The same single step the numpad makes, so only the eight tiles around the force are reachable. Holding anything further
   * away says so rather than silently doing nothing — the gesture worked, the order didn't. Ordering a march across the map
   * is a multi-turn move order, which is a separate mechanism.
   */
  const onTileHold = useCallback(
    (tile: TileCell) => {
      const plan = getStepToTile(tile);

      switch (plan.kind) {
        case "step":
          stepForce.mutate({ unitInstanceId: plan.unitInstanceId, direction: plan.direction });
          break;
        case "multiple-selected":
          onSingleForceOnly();
          break;
        case "too-far":
          openToast("Forces can only be moved to a neighbouring tile for now.", "warning");
          break;
        // Holding with nothing selected, or holding the tile the force already stands on, is not a mistake worth
        // interrupting the player over — it just isn't a move.
        case "nothing-selected":
        case "already-here":
          break;
      }
    },
    [getStepToTile, onSingleForceOnly, openToast, stepForce],
  );

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

  // --- Intelligence: what the player learned while they were not looking ---
  const { data: sightingReports } = useSightingReportsQuery(gameId, playerId, tilesEnabled);
  const { data: knownForces } = useKnownForcesQuery(gameId, playerId, tilesEnabled);
  const { data: currentCampaignTurn } = useCurrentCampaignTurnQuery(gameId, tilesEnabled);
  const { data: gamePlayers } = useGamePlayersQuery(gameId, tilesEnabled);

  // Reports carry ids, not names. Both lookups are resolved here, at read time, so a renamed faction reads correctly in reports raised before the rename.
  const factionNameByPlayerId = useMemo(() => new Map((gamePlayers ?? []).map((player) => [player.playerId, player.factionName])), [gamePlayers]);

  const sightingDescriptionContext = useMemo<SightingDescriptionContext>(
    () => ({ unitNameByUnitTemplateId, factionNameByPlayerId }),
    [unitNameByUnitTemplateId, factionNameByPlayerId],
  );

  // Null while nobody is taking a turn, which draws every known-force marker fresh rather than fading them on a guess.
  const currentRoundNumber = currentCampaignTurn?.currentRoundNumber ?? null;

  // Carries the last build's DTO-to-cell pairs across rebuilds so unchanged tiles keep their object identity. A ref rather
  // than state: it is a memoisation detail of the line below and must never itself cause a render.
  const tileCellSourcesRef = useRef<TileCellSourceMap>(new Map());

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;

    const { grid, sourceByKey } = buildTiles2d(
      gameSelection.width,
      gameSelection.height,
      tilesResponse.tiles,
      tileCellSourcesRef.current,
    );
    tileCellSourcesRef.current = sourceByKey;

    return grid;
  }, [gameSelection, tilesResponse]);

  // The live half of the fog, derived from the observers the tiles cache is holding. Both the map load and a force step write
  // them there, so the map is never left showing whichever of the two spoke last.
  const { observedTileKeys } = useFogOfWar(tilesResponse?.observers);

  const { biomeNameByType, geographyNameByType, getTileStyle, getTileTitle } = useTileRendering({ biomes, geographies, selectedTile, observedTileKeys });

  // Stable per-tile predicate for the grid, so its memoised tiles only re-render when what the player can see actually changes.
  const isTileObserved = useCallback((tile: TileCell) => observedTileKeys.has(`${tile.x},${tile.y}`), [observedTileKeys]);

  // Last-known-position markers, grouped by tile once per change rather than searched per tile. Tiles under live sight are left out, so a marker is never
  // drawn beside what the player can plainly see is really there. Each tile's array keeps its identity until the known forces or the sight change, which is
  // what lets the memoised tiles skip re-rendering.
  const knownForcesByTileKey = useMemo(() => groupKnownForcesByUnobservedTile(knownForces ?? [], observedTileKeys), [knownForces, observedTileKeys]);

  const getKnownForces = useCallback((tile: TileCell) => knownForcesByTileKey.get(`${tile.x},${tile.y}`), [knownForcesByTileKey]);

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

  // Dismiss the Military Advisor and the end-turn prompt when the game closes — their toolbar buttons
  // are disabled without an open game, so an already-open dialog would otherwise be left showing stale,
  // unreopenable state. Watching `gameSelection` rather than hooking `closeGame` covers any future path
  // that clears it.
  useEffect(() => {
    if (gameSelection == null) {
      setShowMilitaryAdvisor(false);
      setEndTurnActions([]);
      setEndTurnArmed(false);
    }
  }, [gameSelection]);

  // --- End turn ---
  const endTurn = useEndTurnMutation(gameId, playerId, {
    onSuccess: (outcome) => {
      if (outcome.turnEnded) {
        // Empty the panel and disarm, so nothing carries over into a turn where it was never checked.
        setEndTurnActions([]);
        setEndTurnArmed(false);
        openToast(outcome.roundEnded ? "Turn ended. A new round has begun." : "Turn ended.", "success");
        return;
      }

      // The turn is still running. The server is reporting what the player could still do, so it goes to the notifications panel and the next press
      // of End Turn is the one that ends it.
      setEndTurnActions(outcome.possibleActions);
      setEndTurnArmed(true);

      const count = outcome.possibleActions.length;
      openToast(
        count === 1
          ? "One thing still needs your attention. Press End Turn again to end anyway."
          : `${count} things still need your attention. Press End Turn again to end anyway.`,
        "warning",
      );
    },
    // The API writes its refusals to be read as they stand, so they go straight to the player rather than being reworded here.
    onError: (err) => openToast(err instanceof Error ? err.message : "Your turn could not be ended.", "danger"),
  });

  // One button, two meanings. Unarmed it asks the server to check first, and anything outstanding comes back to fill the notifications panel; armed
  // it means the player has read that list and wants the turn ended regardless.
  const onEndTurn = useCallback(() => endTurn.mutate(endTurnArmed), [endTurn, endTurnArmed]);

  // Remove in F-18. Ends the turn and brings the client back up as the next player, so one developer can play every seat.
  const { endTurnAndSwitchPlayer, endRoundAndReturnToPlayer, isSwitching } = useDevEndTurnSwitch({ openToast });

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
    // Remove in F-18.
    onEndTurnAndSwitchPlayer: endTurnAndSwitchPlayer,
    onEndRoundAndReturnToPlayer: endRoundAndReturnToPlayer,
    devSwitchInProgress: isSwitching,
  });

  return (
    <NotificationsProvider
      endTurnActions={endTurnActions}
      sightingReports={sightingReports ?? NO_SIGHTING_REPORTS}
      sightingDescriptionContext={sightingDescriptionContext}>
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
          onOpenGame={async (game) => {
            setActiveDialog(null);
            try {
              // Swap the user token for a game token before loading the game, so protected
              // endpoints read this game + player straight from the token.
              //
              // That swap is also the only trustworthy answer to "which player am I here": the server
              // resolved it from the account the token proved. The dialog's listing is a public lobby
              // showing every ruler in the game, so taking the player from the row that was clicked
              // would let whoever is logged in drive whichever ruler they happened to pick.
              const entered = await enterGame(game.gameId);

              if (entered.gameId == null || entered.playerId == null || entered.playerId !== game.yourPlayerId) {
                // The token names a player the row doesn't — an inactive slot, say — so there is no ruler
                // label or campaign offset to draw the board with. Say so rather than opening the map
                // under a guessed identity.
                openToast("You do not hold an active player slot in that game.", "danger");
                return;
              }

              // Identity (gameId, playerId) comes from the token; the rest is display detail off the listing,
              // trusted only because the token has just agreed it names the same player.
              setGameSelection({
                gameId: entered.gameId,
                gameName: game.gameName,
                width: game.mapWidth,
                height: game.mapHeight,
                playerId: entered.playerId,
                rulerLabel: game.yourRulerLabel,
                campaignOffsetId: game.yourCampaignOffsetId,
              });

              openToast(`Opened "${game.gameName}" as ${game.yourRulerLabel}`, "success");
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
                      isTileObserved={isTileObserved}
                      getKnownForces={getKnownForces}
                      currentRoundNumber={currentRoundNumber}
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
