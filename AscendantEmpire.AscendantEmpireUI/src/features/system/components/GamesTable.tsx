// Read-only admin grid of games for the dev System tool. Each row identifies a game, says how full it is and
// where it has got to, and offers a Delete button.
//
// Hand-rolled <table> for the same reason UsersTable is: the shared EditableTable is built around growable
// add/remove-row lists and doesn't fit a fixed admin grid with per-row action buttons. The markup deliberately
// matches UsersTable, since the two views sit one toolbar click apart.

import { Button, Badge, Spinner } from "@/components/ui";

import { GAME_CREATION_STATUS_LABELS, GAME_PLAY_STATUS_LABELS } from "../types/Game";
import type { GameSummary } from "../types/Game";

interface GamesTableProps {
  games: GameSummary[];
  loading: boolean;
  /** Called when an admin asks to delete a game. Confirmation is the caller's job. */
  onDelete: (game: GameSummary) => void;
  /** The game currently being deleted, if any, so its row can show the work in progress. */
  deletingGameId?: number | null;
}

/** Formats an ISO timestamp as a short local date/time, or an em dash when absent. */
function formatDate(value: string | null): string {
  if (value == null) {
    return "—";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

/**
 * Colours for the play-status badge: green while a game is live, amber while it is still filling or being built,
 * grey once it is over, red when its setup failed outright.
 */
function playStatusColors(game: GameSummary): { textColor: string; backgroundColor: string } {
  if (game.creationStatus === "SetupFailed") {
    return { textColor: "#7f1d1d", backgroundColor: "#fecaca" };
  }

  switch (game.playStatus) {
    case "InProgress":
      return { textColor: "#14532d", backgroundColor: "#bbf7d0" };
    case "AwaitingPlayers":
    case "SetupInProgress":
      return { textColor: "#78350f", backgroundColor: "#fde68a" };
    default:
      return { textColor: "#334155", backgroundColor: "#e2e8f0" };
  }
}

export default function GamesTable({ games, loading, onDelete, deletingGameId = null }: GamesTableProps) {
  // While the first load is in flight, show a spinner instead of an empty grid.
  if (loading && games.length === 0) {
    return (
      <div className="flex items-center justify-center gap-2 py-12 text-slate-400">
        <Spinner />
        <span>Loading games…</span>
      </div>
    );
  }

  if (games.length === 0) {
    return <p className="py-12 text-center text-sm text-slate-400">No games found.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-700">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="bg-slate-800 text-left text-slate-300">
            <th className="px-3 py-2 font-semibold">ID</th>
            <th className="px-3 py-2 font-semibold">Name</th>
            <th className="px-3 py-2 font-semibold">Players</th>
            <th className="px-3 py-2 font-semibold">Setup Status</th>
            <th className="px-3 py-2 font-semibold">Game Status</th>
            <th className="px-3 py-2 font-semibold">Created</th>
            <th className="px-3 py-2 font-semibold">Actions</th>
          </tr>
        </thead>

        <tbody>
          {games.map((game) => {
            const colors = playStatusColors(game);
            const deleting = deletingGameId === game.gameId;

            return (
              <tr key={game.gameId} className="border-t border-slate-700 text-slate-200">
                <td className="px-3 py-2 text-slate-400">{game.gameId}</td>
                <td className="px-3 py-2 font-medium">{game.gameName}</td>
                <td className="px-3 py-2 text-slate-300">
                  {/* Claimed out of total, so a half-empty lobby is obvious at a glance. */}
                  {game.claimedPlayerCount} / {game.numberOfPlayers}
                </td>
                <td className="px-3 py-2 text-slate-300">{GAME_CREATION_STATUS_LABELS[game.creationStatus]}</td>
                <td className="px-3 py-2">
                  <Badge
                    label={GAME_PLAY_STATUS_LABELS[game.playStatus]}
                    textColor={colors.textColor}
                    backgroundColor={colors.backgroundColor}
                  />
                </td>
                <td className="px-3 py-2 text-slate-400">{formatDate(game.createdAt)}</td>
                <td className="px-3 py-2">
                  <Button
                    variant="danger"
                    className="px-2 py-1 text-xs"
                    disabled={deleting}
                    onClick={() => onDelete(game)}>
                    {deleting ? "Deleting…" : "Delete"}
                  </Button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
