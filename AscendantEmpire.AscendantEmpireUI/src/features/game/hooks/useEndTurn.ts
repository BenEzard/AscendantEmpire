import { useMutation, useQueryClient } from "@tanstack/react-query";
import { postEndCampaignTurn, type EndCampaignTurnOutcome } from "@/features/game/api/turnApi";
import { playerCampaignTilesKey } from "@/features/map/hooks/useCampaignMapTiles";
import { unitDisplayInfoKey } from "@/features/map/hooks/useUnitDisplayInfo";
import { knownForcesKey, knownStructuresKey, sightingReportsKey } from "@/features/intelligence/hooks/useIntelligenceQueries";
import { TILE_SUSTENANCE_KEY_PREFIX } from "@/features/map/hooks/useTileSustenance";
import { TILE_SUPPORT_KEY_PREFIX } from "@/features/map/hooks/useTileSupportLevels";
import { currentCampaignTurnKey } from "./useCurrentCampaignTurn";
import { campaignPossibleActionsKey } from "./useCampaignPossibleActions";

/**
 * Ends the current player's campaign turn.
 *
 * A turn that actually ends changes more of the world than a single response can carry — the end-of-turn processes run, movement is restored, and the
 * map may have moved on under someone else's turn — so this drops the cached map and unit state and lets it refetch, rather than trying to patch it
 * the way a single force step does.
 *
 * A call that comes back with `turnEnded` false has changed nothing on the server: it only reported what the player still has outstanding. That report
 * is written into the possible-actions cache, which is where the notifications panel reads it from, and every other cache is left exactly as it was.
 */
export function useEndTurnMutation(
  gameId: number | undefined,
  playerId: number | undefined,
  opts?: {
    onSuccess?: (outcome: EndCampaignTurnOutcome) => void;
    onError?: (error: unknown) => void;
  },
) {
  const queryClient = useQueryClient();

  return useMutation<EndCampaignTurnOutcome, unknown, boolean>({
    mutationFn: (force) => postEndCampaignTurn(force),
    onSuccess: (outcome) => {
      if (outcome.turnEnded === false) {
        // The check reported what the player still has outstanding, which is the same thing the possible-actions query holds. Writing it into that cache
        // rather than into state of its own keeps one answer to the question: whichever of the two routes last spoke, the panel is built from it.
        queryClient.setQueryData(campaignPossibleActionsKey(gameId, playerId), {
          isYourTurn: true,
          possibleActions: outcome.possibleActions,
        });
      }

      if (outcome.turnEnded) {
        queryClient.invalidateQueries({ queryKey: playerCampaignTilesKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: unitDisplayInfoKey(gameId) });

        // The toolbar's verdicts are judged against movement and strength, both of which the end of a turn can change. The bare prefix rather than a
        // built key: each selection is cached separately, and any of those entries may hold a force the turn's processes touched.
        queryClient.invalidateQueries({ queryKey: ["forceContextActions"] });

        // The feed's window is measured from the player's last ended turn, which has just moved, and the round may have moved with it. Their own sightings
        // step raised nothing for them, but a feed read before now still holds the reports from before this turn, and those are no longer new.
        queryClient.invalidateQueries({ queryKey: sightingReportsKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: knownForcesKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: knownStructuresKey(gameId, playerId) });
        queryClient.invalidateQueries({ queryKey: currentCampaignTurnKey(gameId) });

        // The turn has passed to somebody else, so whatever the player had outstanding is no longer theirs to do. Invalidated rather than blanked here,
        // so a hot-seat game that comes straight back round to this player is answered by the server rather than by an assumption.
        queryClient.invalidateQueries({ queryKey: campaignPossibleActionsKey(gameId, playerId) });

        // What is left on a tile changes at the end of a turn, when forces feed. The bare prefix: every tile the player has looked at is cached separately.
        queryClient.invalidateQueries({ queryKey: TILE_SUSTENANCE_KEY_PREFIX });

        // The hover marker's support levels are worked out from the same food, so they go stale with it. The bare prefix: each selection is cached separately.
        queryClient.invalidateQueries({ queryKey: TILE_SUPPORT_KEY_PREFIX });
      }

      opts?.onSuccess?.(outcome);
    },
    onError: (error) => opts?.onError?.(error),
  });
}
