import { useState } from "react";
import { GiCrown } from "react-icons/gi";
import Tooltip from "@/components/ui/Tooltip";

/** Rendered height of the portrait, in px. Chosen to fit the toolbar's existing height without growing it. */
const PORTRAIT_HEIGHT = 40;

interface CurrentTurnRulerIndicatorProps {
  /**
   * Root-relative portrait path of the player whose turn is open.
   *
   * Null when no turn is open, and empty for a slot predating the portrait column — both render the fallback glyph.
   */
  portraitPath: string | null;

  /** "Title Name (Faction)" for that player, or null when no turn is open. */
  rulerLabel: string | null;
}

/**
 * Who the campaign is currently waiting on: their ruler's portrait and label, for the game toolbar's trailing region.
 *
 * Presentational on purpose — the join between the open turn and the player it names is the caller's job, so this can
 * be rendered and tested without a game, a query client or a token.
 */
export default function CurrentTurnRulerIndicator({ portraitPath, rulerLabel }: CurrentTurnRulerIndicatorProps) {
  // Remembered per path, so a later turn whose portrait does load is not tarred by an earlier one that didn't.
  const [failedPath, setFailedPath] = useState<string | null>(null);

  // No turn is open. Said in words rather than rendered as nothing, because `GET /turn/campaign` legitimately 404s
  // between turns, and a region of the toolbar that silently vanishes reads as a bug. The region is right-aligned, so
  // showing it costs no movement on the left.
  if (rulerLabel === null) {
    return (
      <span className="flex items-center gap-2 text-sm text-slate-500">
        <span aria-hidden="true" className="rounded border border-dashed border-slate-300" style={{ height: PORTRAIT_HEIGHT, width: PORTRAIT_HEIGHT }} />
        No open turn
      </span>
    );
  }

  const description = `It is ${rulerLabel}'s turn`;
  const showPortrait = portraitPath !== null && portraitPath !== "" && portraitPath !== failedPath;

  return (
    <Tooltip content={description}>
      <span className="flex min-w-0 items-center gap-2" aria-label={description}>
        {showPortrait ? (
          // Decorative: the label beside it already names the player, so a second reading would be noise.
          <img
            src={portraitPath}
            alt=""
            draggable={false}
            style={{ height: PORTRAIT_HEIGHT }}
            className="w-auto rounded border border-slate-300"
            onError={() => setFailedPath(portraitPath)}
          />
        ) : (
          <span
            aria-hidden="true"
            className="flex items-center justify-center rounded border border-slate-300 text-slate-500"
            style={{ height: PORTRAIT_HEIGHT, width: PORTRAIT_HEIGHT }}>
            <GiCrown size={Math.round(PORTRAIT_HEIGHT * 0.6)} />
          </span>
        )}

        <span className="truncate text-sm font-medium text-slate-700">{rulerLabel}</span>
      </span>
    </Tooltip>
  );
}
