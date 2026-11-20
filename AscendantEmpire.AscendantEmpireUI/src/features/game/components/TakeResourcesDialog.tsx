import { useEffect, useState } from "react";
import { Alert, Button, Modal, Spinner } from "@/components/ui";
import MapResourceIcon from "@/components/map/MapResourceIcon";
import type { TakeResourcesOption, TakeResourcesResult } from "@/features/game/api/takeResourcesApi";
import { useTakeResourcesMutation, useTakeResourcesOptionsQuery } from "@/features/game/hooks/useTakeResources";

/** Rendered size of a resource's own image in the list, in px. */
const RESOURCE_ICON_SIZE = 32;

export interface TakeResourcesDialogProps {
  show: boolean;

  /** Scopes the cache entries the take refreshes. The request itself takes both from the game token. */
  gameId: number | undefined;
  playerId: number | undefined;

  /** The whole selection, which takes together. Empty or null closes the dialog. */
  forceIds: readonly number[] | null;

  onClose: () => void;

  /** Called once the take has happened, so the caller can report it. */
  onTaken: (result: TakeResourcesResult) => void;
}

/**
 * Lets the player choose which of the tile's map resources the selection takes from.
 *
 * The dialog exists because a tile can hold two resources and the order covers one: nothing else about the take is the
 * player's to decide, so there is no quantity to set here. What each would yield is the server's answer, and deliberately
 * phrased as what the selection would gain rather than what the tile holds — the true quantity stays behind the player's
 * fuzzed estimate.
 */
export default function TakeResourcesDialog({ show, gameId, playerId, forceIds, onClose, onTaken }: TakeResourcesDialogProps) {
  const [selectedInstanceId, setSelectedInstanceId] = useState<number | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Null while the dialog is closed, which is what keeps the query from firing for a dialog nobody has opened.
  const {
    data: options,
    isPending: isLoadingOptions,
    isError: hasOptionsError,
    error: optionsError,
  } = useTakeResourcesOptionsQuery(show && forceIds != null && forceIds.length > 0 ? forceIds : null);

  const resources = options?.resources ?? [];

  // Preselected when only one resource comes back. The dialog is opened only for a tile holding two, so that is the tile
  // having changed since — but a lone row the player still has to click would be a step that decides nothing.
  useEffect(() => {
    setSelectedInstanceId(resources.length === 1 ? resources[0].mapResourceInstanceId : null);
    setErrorMessage(null);
  }, [show, resources.length, resources[0]?.mapResourceInstanceId]);

  const takeMutation = useTakeResourcesMutation(gameId, playerId, {
    onSuccess: (result) => {
      onTaken(result);
      onClose();
    },
    // The server's own wording. Every refusal here is written to be shown to the player: the resource is gone, the forces
    // have already moved, it is not this player's turn.
    onError: (error) => setErrorMessage(error instanceof Error ? error.message : "The resource could not be taken."),
  });

  const canSubmit = selectedInstanceId != null && forceIds != null && forceIds.length > 0 && !takeMutation.isPending;

  const submit = () => {
    if (!canSubmit || selectedInstanceId == null || forceIds == null) {
      return;
    }

    setErrorMessage(null);
    takeMutation.mutate({ forceIds: [...forceIds], mapResourceInstanceId: selectedInstanceId });
  };

  return (
    <Modal
      open={show}
      title="Take Resources"
      onClose={onClose}
      panelClassName="thick-border-modal"
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="outline-secondary" onClick={onClose} disabled={takeMutation.isPending}>
            Cancel
          </Button>
          <Button variant="primary" onClick={submit} disabled={!canSubmit}>
            {takeMutation.isPending ? "Taking…" : "Take"}
          </Button>
        </div>
      }>
      {isLoadingOptions ? (
        <Spinner />
      ) : hasOptionsError ? (
        <Alert variant="danger">{optionsError instanceof Error ? optionsError.message : "What is here could not be read."}</Alert>
      ) : resources.length === 0 ? (
        <Alert variant="warning">There is nothing left to take here.</Alert>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="text-sm text-slate-600">Taking resources will use all movement points for this turn.</div>

          <div className="flex flex-col gap-2">
            {resources.map((resource) => (
              <TakeResourcesOptionRow
                key={resource.mapResourceInstanceId}
                resource={resource}
                isSelected={resource.mapResourceInstanceId === selectedInstanceId}
                onSelect={() => {
                  setSelectedInstanceId(resource.mapResourceInstanceId);
                  // Dropped on a new pick, so a refusal about the other resource does not sit under this one.
                  setErrorMessage(null);
                }}
              />
            ))}
          </div>

          {errorMessage ? <Alert variant="danger">{errorMessage}</Alert> : null}
        </div>
      )}
    </Modal>
  );
}

interface TakeResourcesOptionRowProps {
  resource: TakeResourcesOption;
  isSelected: boolean;
  onSelect: () => void;
}

/** One resource the selection could take from, and what it would come away with. */
function TakeResourcesOptionRow({ resource, isSelected, onSelect }: TakeResourcesOptionRowProps) {
  const borderClass = isSelected ? "border-sky-600 bg-sky-50" : "border-slate-200 hover:border-slate-400";

  return (
    <label className={`flex cursor-pointer items-center gap-3 rounded border p-2 ${borderClass}`}>
      <input
        type="radio"
        name="takeResourcesOption"
        checked={isSelected}
        onChange={onSelect}
        aria-label={resource.displayName}
      />

      <MapResourceIcon iconPath={resource.iconPath} size={RESOURCE_ICON_SIZE} />

      <div className="flex flex-col text-sm">
        <span className="font-medium">{resource.displayName}</span>
        {/* What it gives, not how much of it: the amount is the same arithmetic for either resource, so it says nothing about
            the choice being made here, and a figure shown before the order is one the dwindling patch may not honour. */}
        <span className="text-slate-600">
          {resource.wouldGain.length === 0
            ? "Nothing to gain"
            : `Gain ${resource.wouldGain.map((line) => line.assetDisplayName).join(", ")}`}
        </span>
      </div>
    </label>
  );
}
