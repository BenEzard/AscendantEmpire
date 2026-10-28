import { useEffect, useMemo, useState } from "react";
import { Alert, Button, Modal, Spinner, dialogTitle } from "@/components/ui";
import type { SplitForceResult } from "@/features/game/api/splitForceApi";
import { useSplitForceMutation, useSplitForceOptionsQuery } from "@/features/game/hooks/useSplitForce";
import SplitForceGroupRow from "./SplitForceGroupRow";
import {
  MINIMUM_SPLIT_DIVISIONS,
  buildSplitAtoms,
  buildSplitGroups,
  createSplitComposition,
  cutGroup,
  hasAnyCut,
  mergeGroups,
  moveAtom,
  type SplitComposition,
} from "./splitForceModel";

export interface SplitForceDialogProps {
  show: boolean;

  /** Scopes the cache entries the split refreshes. The request itself takes both from the game token. */
  gameId: number | undefined;
  playerId: number | undefined;

  /** The Force being split. Null closes the dialog. One force only — one dialog carves one force. */
  forceId: number | null;

  /** The Force's map image, used as the atom icon. */
  unitImageSrc?: string;

  onClose: () => void;

  /** Called once the split has happened, so the caller can report it. */
  onSplit: (result: SplitForceResult) => void;
}

/**
 * Lets the player carve one unled Force into several before committing to any of it.
 *
 * The whole composition lives in memory here — groups are cut, rejoined and rebalanced locally, and nothing is sent until the
 * player confirms. A Force can therefore be divided into any combination of its base quanta (200 in eights of 25 → 25 + 75 + 100,
 * and so on), either by cutting it where the player wants the division or by dragging quanta between the groups.
 *
 * The quantum itself comes from the server, which is what makes the dialog correct for every unled organisation rather than
 * only the Settler.
 */
export default function SplitForceDialog({ show, gameId, playerId, forceId, unitImageSrc, onClose, onSplit }: SplitForceDialogProps) {
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Null while the dialog is closed, which is what keeps the query from firing for a dialog nobody has opened.
  const {
    data: options,
    isPending: isLoadingOptions,
    isError: hasOptionsError,
    error: optionsError,
  } = useSplitForceOptionsQuery(show ? forceId : null);

  const totalStrength = options?.totalStrength ?? 0;
  const injuredCount = options?.injuredCount ?? 0;
  const baseSize = options?.baseSize ?? 0;

  // The Force's current strength is what splits — the wounded travel with their group, they aren't left behind.
  const atoms = useMemo(() => buildSplitAtoms(totalStrength, baseSize), [totalStrength, baseSize]);

  const [composition, setComposition] = useState<SplitComposition>(() => createSplitComposition(atoms.length));

  // Start from an undivided Force each time the dialog opens, and whenever the Force behind it changes shape. A
  // part-finished split from a previous opening would be actively misleading.
  useEffect(() => {
    setComposition(createSplitComposition(atoms.length));
    setErrorMessage(null);
  }, [show, forceId, atoms.length]);

  const groups = useMemo(() => buildSplitGroups(composition, atoms, injuredCount), [composition, atoms, injuredCount]);

  const splitMutation = useSplitForceMutation(gameId, playerId, {
    onSuccess: (result) => {
      onSplit(result);
      onClose();
    },
    // The server's own wording. Every refusal here is written to be shown to the player: the force has already moved, the
    // sizes are not a legal division.
    onError: (error) => setErrorMessage(error instanceof Error ? error.message : "The force could not be split."),
  });

  const canSplitAtAll = atoms.length >= MINIMUM_SPLIT_DIVISIONS;
  const isDivided = hasAnyCut(composition);
  const isSplitReady = isDivided && !splitMutation.isPending;

  const submit = () => {
    if (forceId == null || !isSplitReady) {
      return;
    }

    setErrorMessage(null);
    // Sizes only. Where the wounded end up is the server's call, by the same apportionment the preview above used.
    splitMutation.mutate({ unitInstanceId: forceId, groupSizes: groups.map((group) => group.size) });
  };

  return (
    <Modal
      open={show}
      // "Split Force: Settler (Unled) - 200" — what is being divided, how it is led, and the strength the division has to begin with.
      title={dialogTitle(
        "Split Force",
        options == null ? null : `${options.displayName} (${options.leadershipModel}) - ${options.totalStrength.toLocaleString()}`,
      )}
      onClose={onClose}
      size="lg"
      panelClassName="thick-border-modal"
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="outline-secondary" onClick={onClose} disabled={splitMutation.isPending}>
            Cancel
          </Button>
          <Button variant="primary" onClick={submit} disabled={!isSplitReady}>
            {splitMutation.isPending ? "Splitting…" : isDivided ? `Split into ${groups.length} forces` : "Split"}
          </Button>
        </div>
      }>
      {isLoadingOptions ? (
        <Spinner />
      ) : hasOptionsError ? (
        <Alert variant="danger">{optionsError instanceof Error ? optionsError.message : "This force's split options could not be read."}</Alert>
      ) : !canSplitAtAll ? (
        <Alert variant="warning">
          {options?.displayName} has {totalStrength.toLocaleString()} soldiers and its smallest group is {baseSize.toLocaleString()}, so there is
          nothing to divide — a split needs at least {(baseSize * MINIMUM_SPLIT_DIVISIONS).toLocaleString()}.
        </Alert>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1 text-sm text-slate-600">
            <div>Divide forces by clicking between them, drag and drop forces to move them between splits, recombine by pressing the plus icon.</div>
            <div>Splitting the force will use all movement points for this turn.</div>
          </div>

          <SplitForceGroupRow
            groups={groups}
            atoms={atoms}
            unitName={options?.displayName ?? ""}
            unitImageSrc={unitImageSrc}
            onCut={(groupId, atomIndex) => setComposition((current) => cutGroup(current, groupId, atomIndex))}
            onMerge={(groupId, otherGroupId) => setComposition((current) => mergeGroups(current, groupId, otherGroupId))}
            onMoveAtom={(atomIndex, targetGroupId) => setComposition((current) => moveAtom(current, atomIndex, targetGroupId))}
          />

          {errorMessage ? <Alert variant="danger">{errorMessage}</Alert> : null}
        </div>
      )}
    </Modal>
  );
}
