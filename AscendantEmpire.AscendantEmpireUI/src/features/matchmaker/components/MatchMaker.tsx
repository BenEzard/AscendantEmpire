import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Badge, Button, Spinner, Toast, ToastViewport, Tooltip } from "@/components/ui";
import { enterGame } from "@/features/auth/api/authApi";
import { getMatchMakingGames } from "../api/matchMakingApi";
import { claimPlayerSlot } from "../api/matchMakingApi";
import type { MatchMakingGamesResponse } from "../types/MatchMakingGamesResponse";
import type { MatchMakingGame } from "../types/MatchMakingGame";
import type { MatchMakingSlot } from "../types/MatchMakingSlot";

// react-query key for the joinable-games list, shared by the fetch and the claim invalidation.
const GAMES_QUERY_KEY = ["matchMaking", "games"];

type ToastState = {
  message: string;
  variant: "success" | "danger";
};

/**
 * The MatchMaker landing screen (shown once logged in via AuthGate). Lists joinable games and
 * their player slots, lets the user claim a free slot, then jump straight into a slot they hold.
 */
export default function MatchMaker() {
  const queryClient = useQueryClient();

  const [filter, setFilter] = useState("");
  const [toast, setToast] = useState<ToastState | null>(null);
  // Tracks which slot is mid-claim / mid-enter so we can show a per-row spinner and disable it.
  const [busyPlayerId, setBusyPlayerId] = useState<number | null>(null);

  const { data, isLoading, isFetching, error, refetch } = useQuery<MatchMakingGamesResponse, Error>({
    queryKey: GAMES_QUERY_KEY,
    queryFn: getMatchMakingGames,
    refetchOnWindowFocus: false,
  });

  const games = useMemo<MatchMakingGame[]>(() => data?.games ?? [], [data]);

  // Filter on game name or any slot's ruler label, mirroring the open-game dialog's behaviour.
  const filteredGames = useMemo(() => {
    const needle = filter.trim().toLowerCase();
    if (!needle) {
      return games;
    }

    return games.filter((game) => {
      if (game.gameName.toLowerCase().includes(needle)) {
        return true;
      }
      return game.slots.some((slot) => slot.rulerLabel.toLowerCase().includes(needle));
    });
  }, [games, filter]);

  const claimMutation = useMutation({
    mutationFn: ({ gameId, playerId }: { gameId: number; playerId: number }) => claimPlayerSlot(gameId, playerId),
    onMutate: ({ playerId }) => {
      setBusyPlayerId(playerId);
    },
    onSuccess: async (_data, variables) => {
      // Re-fetch so the freshly claimed slot now shows as "your slot" with a Play button.
      await queryClient.invalidateQueries({ queryKey: GAMES_QUERY_KEY });
      const game = games.find((g) => g.gameId === variables.gameId);
      const slot = game?.slots.find((s) => s.playerId === variables.playerId);
      setToast({ message: `Claimed ${slot?.rulerLabel ?? "the slot"}. You're in!`, variant: "success" });
    },
    onError: (err: Error) => {
      setToast({ message: err.message, variant: "danger" });
    },
    onSettled: () => {
      setBusyPlayerId(null);
    },
  });

  // "Play now": swap the user token for a game token, then hand off to the game subsystem.
  const handlePlay = async (gameId: number, playerId: number) => {
    setBusyPlayerId(playerId);
    try {
      await enterGame(gameId);
      window.location.href = "/game/";
    } catch (err) {
      setToast({ message: err instanceof Error ? err.message : "Couldn't enter the game.", variant: "danger" });
      setBusyPlayerId(null);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 px-4 py-8">
      <div className="mx-auto w-full max-w-3xl">
        <header className="mb-6">
          <h1 className="text-2xl font-semibold text-slate-800">Match Maker</h1>
          <p className="text-sm text-slate-500">Claim a player slot in an open game, then jump in and play.</p>
        </header>

        <div className="mb-4 flex items-center gap-2">
          <input
            // Explicit bg/text colours: the global :root default text colour is white, so without
            // these the typed value would be white-on-white and invisible.
            className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900"
            placeholder="Filter by game or ruler..."
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
          <Button variant="outline-secondary" onClick={() => refetch()} disabled={isLoading || isFetching}>
            Refresh
          </Button>
        </div>

        {error && (
          <Alert variant="danger" className="mb-4">
            Failed to load games: {error.message}
          </Alert>
        )}

        {isLoading ? (
          <div className="flex items-center gap-2 text-slate-600">
            <Spinner size="sm" />
            <span>Loading games...</span>
          </div>
        ) : filteredGames.length === 0 ? (
          <div className="rounded-md border border-slate-200 bg-white px-4 py-6 text-center text-slate-500">
            No joinable games found.
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            {filteredGames.map((game) => (
              <GameCard
                key={game.gameId}
                game={game}
                busyPlayerId={busyPlayerId}
                onClaim={(playerId) => claimMutation.mutate({ gameId: game.gameId, playerId })}
                onPlay={(playerId) => handlePlay(game.gameId, playerId)}
              />
            ))}
          </div>
        )}
      </div>

      <ToastViewport>
        {toast && (
          <Toast open message={toast.message} variant={toast.variant} onClose={() => setToast(null)} />
        )}
      </ToastViewport>
    </div>
  );
}

interface GameCardProps {
  game: MatchMakingGame;
  busyPlayerId: number | null;
  onClaim: (playerId: number) => void;
  onPlay: (playerId: number) => void;
}

/** A single game with its slots laid out as claimable / taken / "your slot" rows. */
function GameCard({ game, busyPlayerId, onClaim, onPlay }: GameCardProps) {
  // A user may hold only one slot per game, so once they've claimed one here, the rest lock.
  const userHasSlotHere = game.slots.some((slot) => slot.isClaimedByCurrentUser);
  const claimedCount = game.slots.filter((slot) => slot.isClaimed).length;

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-baseline justify-between">
        <h2 className="text-lg font-semibold text-slate-800">{game.gameName}</h2>
        <span className="text-xs text-slate-500">
          {game.mapWidth}×{game.mapHeight} • {claimedCount}/{game.slots.length} slots taken
        </span>
      </div>

      <ul className="divide-y divide-slate-100">
        {game.slots.map((slot) => (
          <SlotRow
            key={slot.playerId}
            slot={slot}
            userHasSlotHere={userHasSlotHere}
            isBusy={busyPlayerId === slot.playerId}
            onClaim={() => onClaim(slot.playerId)}
            onPlay={() => onPlay(slot.playerId)}
          />
        ))}
      </ul>
    </div>
  );
}

interface SlotRowProps {
  slot: MatchMakingSlot;
  userHasSlotHere: boolean;
  isBusy: boolean;
  onClaim: () => void;
  onPlay: () => void;
}

function SlotRow({ slot, userHasSlotHere, isBusy, onClaim, onPlay }: SlotRowProps) {
  return (
    <li className="flex items-center justify-between gap-3 py-2">
      <span className="text-sm text-slate-700">{slot.rulerLabel}</span>

      {slot.isClaimedByCurrentUser ? (
        // The user's own seat: badge it and offer to jump straight in.
        <div className="flex items-center gap-2">
          <Badge label="Your slot" textColor="#fff" backgroundColor="var(--bs-success)" />
          <Button variant="primary" className="px-3 py-1 text-xs" onClick={onPlay} disabled={isBusy}>
            {isBusy ? <Spinner size="sm" /> : "Play now"}
          </Button>
        </div>
      ) : slot.isClaimed ? (
        // Someone else holds it — shown as unavailable without revealing who.
        <Badge label="Taken" textColor="#475569" backgroundColor="#e2e8f0" />
      ) : (
        // A free seat: claimable unless the user already holds a slot in this game.
        <Tooltip content={userHasSlotHere ? "You already hold a slot in this game" : undefined}>
          <span>
            <Button
              variant="outline-primary"
              className="px-3 py-1 text-xs"
              onClick={onClaim}
              disabled={isBusy || userHasSlotHere}>
              {isBusy ? <Spinner size="sm" /> : "Claim"}
            </Button>
          </span>
        </Tooltip>
      )}
    </li>
  );
}
