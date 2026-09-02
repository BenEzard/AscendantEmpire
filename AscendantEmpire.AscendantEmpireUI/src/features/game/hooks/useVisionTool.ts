import { useCallback, useEffect, useRef, useState } from "react";
import { useClearPlayerCampaignMapMutation, useLoadPlayerCampaignMapMutation } from "@/features/map/hooks/useCampaignMapTiles";
import type { LoadPlayerCampaignMapRequest } from "@/features/map/api/playerCampaignMapApi";
import type { ToastVariant } from "@/hooks/useToast";

type VisionActiveTool = "vision" | null;
type SelectedTile = { x: number; y: number };

interface UseVisionToolOptions {
  gameId: number | undefined;
  playerId: number | undefined;
  openToast: (message: string, variant?: ToastVariant) => void;
}

/**
 * Owns the "check vision" dev tool: the active state, the selected tile, the vision settings, and the request/clear mutations.
 * Requests are de-duplicated via refs so toggling the tool or re-selecting the same tile doesn't refire an identical load.
 *
 * It no longer owns the fog. The observers its load returns go into the tiles cache like any other view of the map, and the fog
 * is derived from there by {@link useFogOfWar}. They used to be held here as tool state, which meant the map was only ever
 * un-greyed by poking this dev tool: opening a game left the whole board under fog however much of it the player had explored.
 */
export function useVisionTool({ gameId, playerId, openToast }: UseVisionToolOptions) {
  const [activeTool, setActiveTool] = useState<VisionActiveTool>(null);
  const [selectedTile, setSelectedTile] = useState<SelectedTile | null>(null);
  const [showVisionSettings, setShowVisionSettings] = useState(false);
  const [visionRadius, setVisionRadius] = useState<number>(0);
  const [visionLoadWholeMap, setVisionLoadWholeMap] = useState<boolean>(false);

  const visionLastRequestKeyRef = useRef<string | null>(null);
  const visionInFlightRef = useRef(false);
  const visionMutateRef = useRef<null | ((payload: LoadPlayerCampaignMapRequest) => void)>(null);
  const prevActiveToolRef = useRef<VisionActiveTool>(null);

  // Reset vision state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setActiveTool(null);
      setSelectedTile(null);
    }
  }, [gameId]);

  // Tile selection should ONLY exist while Vision is active.
  useEffect(() => {
    if (activeTool !== "vision") setSelectedTile(null);
  }, [activeTool]);

  const toggleVisionTool = useCallback(() => {
    setActiveTool((t) => (t === "vision" ? null : "vision"));
  }, []);

  const openVisionSettings = useCallback(() => {
    setShowVisionSettings(true);
  }, []);

  const clearPlayerCampaignMap = useClearPlayerCampaignMapMutation({
    onSuccess: () => {
      openToast("Vision cleared", "info");
    },
    onError: (error) => {
      const msg = error instanceof Error ? error.message : "Unknown error";
      openToast(`Clear vision failed: ${msg}`, "danger");
    },
  });

  const clearVision = useCallback(() => {
    setActiveTool(null);

    // Nothing to clear locally: the mutation invalidates the tiles query, and the refetch brings back a map on which the
    // server has forgotten everything, with every tile unexplored and no observers on it.
    if (gameId == null || playerId == null) return;
    clearPlayerCampaignMap.mutate({ gameId, playerId });
  }, [clearPlayerCampaignMap, gameId, playerId]);

  const loadPlayerCampaignMap = useLoadPlayerCampaignMapMutation({
    // The mutation has already merged the response into the tiles cache, observers and all, which is where the fog reads them
    // from. There is nothing left to apply here.
    onSuccess: () => {
      visionInFlightRef.current = false;
    },
    onError: (error) => {
      visionInFlightRef.current = false;
      const msg = error instanceof Error ? error.message : "Unknown error";
      openToast(`Vision load failed: ${msg}`, "danger");
    },
  });

  useEffect(() => {
    visionMutateRef.current = loadPlayerCampaignMap.mutate;
  }, [loadPlayerCampaignMap.mutate]);

  const sendVisionRequest = useCallback(
    (x: number, y: number) => {
      if (activeTool !== "vision") return;
      if (gameId == null || playerId == null) return;

      const requestKeyWithRadius = `${gameId}:${playerId}:${x},${y}:r${visionRadius}`;
      const requestKey = `${requestKeyWithRadius}:whole${visionLoadWholeMap ? 1 : 0}`;

      if (visionInFlightRef.current && visionLastRequestKeyRef.current === requestKey) return;
      if (visionLastRequestKeyRef.current === requestKey) return;

      const payload: LoadPlayerCampaignMapRequest = {
        gameId,
        playerId,
        loadWholeMap: visionLoadWholeMap,
      };

      visionLastRequestKeyRef.current = requestKey;
      visionInFlightRef.current = true;

      const mutate = visionMutateRef.current;
      if (!mutate) {
        visionInFlightRef.current = false;
        return;
      }

      mutate(payload);
    },
    [activeTool, gameId, playerId, visionRadius, visionLoadWholeMap],
  );

  // Re-send the last vision request when the tool transitions into "vision".
  useEffect(() => {
    const prev = prevActiveToolRef.current;
    prevActiveToolRef.current = activeTool;

    if (activeTool !== "vision") return;
    if (prev === "vision") return;
    if (!selectedTile) return;

    sendVisionRequest(selectedTile.x, selectedTile.y);
  }, [activeTool, selectedTile, sendVisionRequest]);

  return {
    activeTool,
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
  };
}
