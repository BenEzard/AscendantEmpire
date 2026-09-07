import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Modal, Spinner, Tooltip } from "@/components/ui";
import type { MyGamesResponse } from "../types/MyGamesResponse";
import type { MyGame } from "../types/MyGame";
import { getMyGames } from "../hooks/getMyGames";

export interface OpenGameDialogProps {
  show: boolean;
  onClose: () => void;
  /**
   * Hands back the game the user picked — the game only, never a player.
   *
   * Which player the user controls in it is the token's answer, not this dialog's: the caller enters
   * the game and takes the player from what the server issues. The row does carry the caller's own slot,
   * but only as display detail to draw the board with once the token has agreed it is the same player.
   */
  onOpenGame: (game: MyGame) => void;
}

/**
 * Picks one of the user's own games to open.
 *
 * The listing is scoped to the logged-in account, so every row is a game they hold an active player slot
 * in — there is no lobby of other people's games to wade through. Each game is a single row, and the
 * Campaign Turn column names whoever the campaign queue is currently waiting on, which is the one thing
 * that actually distinguishes "the game I can play now" from "the game I'm waiting on".
 */
export default function OpenGameDialog({ show, onClose, onOpenGame }: OpenGameDialogProps) {
  const [filter, setFilter] = useState("");
  const [selectedGameId, setSelectedGameId] = useState<number | null>(null);

  const { data, isLoading, isFetching, error, refetch } = useQuery<MyGamesResponse, Error>({
    queryKey: ["games", "mine"],
    queryFn: getMyGames,
    enabled: show,
    refetchOnWindowFocus: false,
  });

  const games = useMemo<MyGame[]>(() => data?.games ?? [], [data]);

  useEffect(() => {
    if (!show) {
      return;
    }

    setFilter("");
    setSelectedGameId(null);
  }, [show]);

  const filteredGames = useMemo(() => {
    const f = filter.trim().toLowerCase();
    if (!f) {
      return games;
    }

    return games.filter((g) => {
      if (g.gameName.toLowerCase().includes(f)) {
        return true;
      }
      return g.currentTurnRulerLabel?.toLowerCase().includes(f) ?? false;
    });
  }, [games, filter]);

  const selectedGame = useMemo<MyGame | null>(() => {
    if (selectedGameId == null) {
      return null;
    }

    return games.find((g) => g.gameId === selectedGameId) ?? null;
  }, [selectedGameId, games]);

  const handleOpen = () => {
    if (!selectedGame) {
      return;
    }

    onOpenGame(selectedGame);
    onClose();
  };

  return (
    <Modal
      open={show}
      onClose={onClose}
      title="Open Game"
      size="lg"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleOpen} disabled={!selectedGame}>
            Open
          </Button>
        </>
      }>
      <div className="flex items-center gap-2 mb-3">
        <input
          className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
          placeholder="Filter by game or ruler..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        <Button variant="outline-secondary" onClick={() => refetch()} disabled={isLoading || isFetching}>
          Refresh
        </Button>
      </div>

      {error && (
        <Alert variant="danger" className="mb-3">
          Failed to load games: {error.message}
        </Alert>
      )}

      {isLoading ? (
        <div className="flex items-center gap-2 text-slate-600">
          <Spinner size="sm" />
          <span>Loading games...</span>
        </div>
      ) : (
        <div className="max-h-[420px] overflow-auto rounded-md border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-700">
              <tr>
                <th className="px-3 py-2" style={{ width: "35%" }}>
                  Game
                </th>
                <th className="px-3 py-2" style={{ width: 120 }}>
                  Size
                </th>
                <th className="px-3 py-2">Campaign Turn</th>
              </tr>
            </thead>

            <tbody>
              {filteredGames.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-3 py-3 text-slate-500">
                    You are not currently in any games.
                  </td>
                </tr>
              ) : (
                filteredGames.map((g) => {
                  const isSelected = selectedGameId === g.gameId;

                  // Every row is a game this account holds an active slot in, so every row can be opened.
                  // The player it opens as still comes back from the server on entering, not from here.
                  const onClick = () => {
                    setSelectedGameId(g.gameId);
                  };

                  const onDoubleClick = () => {
                    setSelectedGameId(g.gameId);
                    onOpenGame(g);
                    onClose();
                  };

                  return (
                    <tr
                      key={g.gameId}
                      className={[
                        "border-t border-slate-100",
                        "cursor-pointer hover:bg-slate-50",
                        isSelected ? "bg-slate-100" : "",
                      ].join(" ")}
                      role="button"
                      onClick={onClick}
                      onDoubleClick={onDoubleClick}>
                      <td className="px-3 py-2 align-top">
                        <div className="font-semibold">{g.gameName}</div>
                      </td>

                      <td className="px-3 py-2 align-top">
                        {g.mapWidth}×{g.mapHeight}
                      </td>

                      <td className="px-3 py-2">
                        <Tooltip content="Double-click to open this game">
                          <span className="inline-flex items-center gap-2">
                            {/* No ruler to name means nobody is taking a turn right now — a game between turns, or
                                one whose campaign queue has yet to start. Both are normal, so this reads as a state
                                rather than as missing data. */}
                            {g.currentTurnRulerLabel ? (
                              <span>{g.currentTurnRulerLabel}</span>
                            ) : (
                              <span className="text-slate-400 italic">Awaiting turn start</span>
                            )}

                            {g.isYourTurn && (
                              <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700">
                                Your turn
                              </span>
                            )}
                          </span>
                        </Tooltip>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}
