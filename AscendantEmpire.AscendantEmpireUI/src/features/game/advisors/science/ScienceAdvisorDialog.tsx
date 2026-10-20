import { useCallback, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";

import { Alert, Button, PagedDialog, Spinner } from "@/components/ui";
import { apiErrorMessages } from "@/util/apiErrors";
import { playerResearchKey, usePlayerResearchQuery } from "@/features/game/hooks/usePlayerResearch";
import AvailableResearchTable from "./AvailableResearchTable";
import { setResearchProject, type PlayerResearchOverview, type ResearchableTechnology } from "./scienceApi";

/** The researchable list stood in for while one is still loading. A module constant, so the empty case keeps a stable reference. */
const NO_TECHNOLOGIES: ResearchableTechnology[] = [];

/**
 * The height the section content is pinned to.
 *
 * Pinned rather than left to size itself so the dialog does not grow taller as a game goes on: what a player can research starts at six
 * technologies and widens with every discovery, and an unpinned page would eventually be taller than the viewport.
 *
 * Set just under two rows of cards at four across, so a second row is visibly cut off rather than either wasting space below a single row or
 * hiding that there is more to scroll to.
 */
const CONTENT_HEIGHT_PX = 300;

export interface ScienceAdvisorDialogProps {
  show: boolean;
  onClose: () => void;
  /** The game the dialog is reading for. Scopes the query cache only; the request takes it from the game token. */
  gameId?: number;
  /** The player the dialog is reading for. Scopes the query cache only; the request takes it from the game token. */
  playerId?: number;
  /**
   * A research position to display instead of fetching one.
   *
   * The app leaves it out and the dialog self-loads. Tests pass it, because there is no network mocking in this project — an injected payload
   * is how a component here gets exercised at all, the same seam TechnologyTreeViewer uses.
   */
  research?: PlayerResearchOverview;
}

/**
 * Science Advisor dialog.
 *
 * Opened from the Advisors group of the game toolbar. Shows what the player could research now and lets them choose a project; selecting one
 * also settles a free technology's acquisition cost, which is what directed research needs before it can begin.
 *
 * A PagedDialog with a single Research section, matching the Military Advisor. The tab shows its title, so the section carries no icon (an
 * icon would replace the label), and the nav column is sized to that label rather than the default third of the dialog. Research projects or
 * specialists can be added as further sections without restructuring anything.
 *
 * Where things sit: the dialog header holds what is true of the player's research whatever page is open — the current project, the banked
 * points, and any error — while the section holds the choosing itself.
 */
export default function ScienceAdvisorDialog({ show, onClose, gameId, playerId, research }: ScienceAdvisorDialogProps) {
  const queryClient = useQueryClient();

  /** The technology a selection is in flight for, so only its own button reads as busy. */
  const [selectingTechnologyId, setSelectingTechnologyId] = useState<number | null>(null);

  const [selectionErrors, setSelectionErrors] = useState<string[]>([]);

  // Called unconditionally, and switched off rather than skipped, because a hook behind a condition is a hook that breaks on the render where
  // the condition changes. Only fetched while the dialog is open: a closed advisor has no reason to hold a round-trip, and reopening it is
  // exactly when the figures want refreshing anyway.
  const researchQuery = usePlayerResearchQuery(gameId, playerId, show && research === undefined);

  const overview = research ?? researchQuery.data;
  const isLoading = research === undefined && researchQuery.isLoading;
  const loadErrors = research !== undefined || researchQuery.error === null ? [] : apiErrorMessages(researchQuery.error);

  const selectProject = useMutation({
    mutationFn: (technologyId: number) => setResearchProject(technologyId),
    onMutate: (technologyId: number) => {
      setSelectingTechnologyId(technologyId);
      setSelectionErrors([]);
    },
    onError: (error: Error) => setSelectionErrors(apiErrorMessages(error)),
    onSuccess: () => {
      // Refetched rather than patched in place: choosing a technology may also have unlocked it, so its status and the round its cost was
      // settled both come back changed, and the server is the authority on what they now are.
      void queryClient.invalidateQueries({ queryKey: playerResearchKey(gameId, playerId) });
    },
    onSettled: () => setSelectingTechnologyId(null),
  });

  const onSelect = useCallback((technologyId: number) => selectProject.mutate(technologyId), [selectProject]);

  const currentProjectLabel =
    overview?.currentTechnologyDisplayName ?? (overview?.currentTechnologyId == null ? "Nothing selected" : `Technology ${overview.currentTechnologyId}`);

  const researchable = overview?.researchable ?? NO_TECHNOLOGIES;

  return (
    <PagedDialog
      open={show}
      title="Science Advisor"
      // Wider than size="xl" (max-w-6xl, 1152px) so four cards across have room to be legible rather than merely to fit. Still a max —
      // Modal keeps the panel w-full, so it shrinks with the viewport and the grid drops to three columns and then two on its own.
      width={1400}
      height={CONTENT_HEIGHT_PX}
      // The nav column hugs its widest label instead of taking the default third of the dialog, which one tab reading "Research (6)" would
      // leave almost entirely empty — and would cost the card grid a column.
      sidebarWidth="content"
      panelClassName="thick-border-modal"
      onCancel={onClose}
      footer={
        // Carries a test id because the Modal's own header × is also labelled "Close", so the role and name alone cannot tell the two apart.
        <Button variant="secondary" onClick={onClose} data-testid="science-advisor-close">
          Close
        </Button>
      }
      header={
        <div className="mb-3 flex flex-col gap-2" data-testid="science-advisor">
          {/* Stated above the pages rather than only marked on a card, so "what am I researching?" is answerable without reading the grid —
              and stays answerable from whatever page is open once there is more than one. */}
          <div className="flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm">
            <span className="text-slate-600">
              Researching: <span className="font-medium text-slate-900">{currentProjectLabel}</span>
            </span>

            {overview !== undefined && (
              <>
                <span className="text-slate-600">
                  Unspent directed points: <span className="font-mono text-slate-900">{overview.unspentDirectedPoints.toLocaleString()}</span>
                </span>
                <span className="text-slate-600">
                  Round <span className="font-mono text-slate-900">{overview.currentRoundNumber}</span>
                </span>
              </>
            )}
          </div>

          {/* Where the figure above comes from and what becomes of it, said plainly: both halves are invisible in the grid, and the second
              is the one that costs a player points if they do not know it. */}
          <p className="text-xs text-slate-500">
            Directed points are produced at the start of your turn and any remaining are spent on your chosen project when the turn ends.
          </p>

          {loadErrors.length > 0 && (
            <Alert variant="danger">
              <ul className="list-inside list-disc">
                {loadErrors.map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            </Alert>
          )}

          {/* A failed selection is reported here rather than on the card, because the refusal is about the choice (a prerequisite unmet, a
              cost that cannot be paid) and not about that card's figures. */}
          {selectionErrors.length > 0 && (
            <Alert variant="danger">
              <ul className="list-inside list-disc">
                {selectionErrors.map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            </Alert>
          )}
        </div>
      }
      sections={[
        {
          key: "research",
          label: "Research",
          // Deliberately no icon. PagedDialog puts an icon *in place of* the label and moves the label to a tooltip, so the title is only
          // visible on the tab while the section carries no icon — which is also why sidebarWidth is "content" above.
          //
          // How many technologies the page is offering, shown as "Research (6)", so the count is readable without opening the page once
          // there is a second page to be on.
          count: researchable.length,
          content: isLoading ? (
            <div className="flex items-center justify-center gap-2 p-4 text-sm text-slate-500">
              <Spinner />
              Loading your research…
            </div>
          ) : loadErrors.length > 0 ? (
            // The header already carries the error; repeating it here would say the same thing twice on one screen.
            <></>
          ) : (
            <AvailableResearchTable technologies={researchable} onSelect={onSelect} selectingTechnologyId={selectingTechnologyId} />
          ),
        },
      ]}
    />
  );
}
