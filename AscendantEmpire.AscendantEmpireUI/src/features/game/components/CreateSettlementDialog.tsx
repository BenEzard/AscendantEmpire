import { useEffect, useState } from "react";
import { Alert, Button, Modal, errorClass } from "@/components/ui";
import type { FoundableSettlementTier, SettleForceResult } from "@/features/game/api/settleForceApi";
import { useSettleForceMutation } from "@/features/game/hooks/useSettleForce";

/**
 * The longest name the server will store, mirroring `Settlement.NAME_MAX_LENGTH`.
 *
 * Duplicated deliberately rather than fetched: it caps the input so the player cannot type a name that would then be refused,
 * and a round-trip to learn one number would make the dialog unopenable while it was in flight. The server still enforces it.
 */
const SETTLEMENT_NAME_MAX_LENGTH = 30;

/** What each tier is called in the dialog's own text. The server owns the rules; this owns the wording. */
const TIER_LABELS: Record<FoundableSettlementTier, string> = {
  Camp: "camp",
  Village: "village",
};

export interface CreateSettlementDialogProps {
  show: boolean;
  /** Scopes the cache entries the founding refreshes. The request itself takes both from the game token. */
  gameId: number | undefined;
  playerId: number | undefined;
  /** Which tier the toolbar button asked for. Null closes the dialog. Only ever a named tier — a camp is founded outright. */
  tier: FoundableSettlementTier | null;
  /** The settler being consumed. One force only — a settlement takes one name. */
  unitInstanceId: number | null;
  onClose: () => void;
  /** Called once the settlement exists, so the caller can refresh the map and report it. */
  onFounded: (result: SettleForceResult) => void;
}

/**
 * Names a new settlement and founds it, consuming the settler.
 *
 * The dialog exists because the founding needs one thing the toolbar button cannot supply: a name. Everything else is the server's to judge.
 */
export default function CreateSettlementDialog({
  show,
  gameId,
  playerId,
  tier,
  unitInstanceId,
  onClose,
  onFounded,
}: CreateSettlementDialogProps) {
  const [name, setName] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Cleared on open rather than on close, so a refusal stays readable while the dialog is shutting and a reopened dialog never
  // starts with the last attempt's name or error still in it.
  useEffect(() => {
    if (show) {
      setName("");
      setErrorMessage(null);
    }
  }, [show]);

  const tierLabel = tier == null ? "settlement" : TIER_LABELS[tier];

  const settleMutation = useSettleForceMutation(gameId, playerId, {
    onSuccess: (result) => {
      onFounded(result);
      onClose();
    },
    onError: (error) => {
      // The server's own wording. Every refusal here is written to be shown to the player: the name is taken, the force has no
      // movement left, the ground cannot be settled.
      setErrorMessage(error instanceof Error ? error.message : "The settlement could not be founded.");
    },
  });

  // Enabled whatever has been typed, so an empty name is refused with a reason rather than by a button that will not press.
  // A player staring at a dead button has to work out what is wrong with it; this tells them.
  const canSubmit = tier != null && unitInstanceId != null && !settleMutation.isPending;

  const submit = () => {
    if (!canSubmit || tier == null || unitInstanceId == null) {
      return;
    }

    // The only check made here. What a name may be — its length, whether it is taken — is the server's to judge, and it
    // judges what it is sent: the trimming is its own, so what is stored is what the refusals are written about.
    if (name.trim().length === 0) {
      setErrorMessage(`Give the ${tierLabel} a name.`);

      return;
    }

    setErrorMessage(null);
    settleMutation.mutate({ unitInstanceId, tier, name });
  };

  return (
    <Modal
      open={show}
      title={`Found a ${tierLabel}`}
      onClose={onClose}
      panelClassName="thick-border-modal"
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="outline-secondary" onClick={onClose} disabled={settleMutation.isPending}>
            Cancel
          </Button>
          <Button variant="primary" onClick={submit} disabled={!canSubmit}>
            {settleMutation.isPending ? "Founding…" : `Found ${tierLabel}`}
          </Button>
        </div>
      }>
      <div className="flex flex-col gap-3">
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium">Name the {tierLabel}</span>
          <input
            className={errorClass(errorMessage != null)}
            value={name}
            maxLength={SETTLEMENT_NAME_MAX_LENGTH}
            placeholder={`What will this ${tierLabel} be called?`}
            autoFocus
            // Enter submits, since the form is one field and reaching for the button is the slower path.
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                event.preventDefault();
                submit();
              }
            }}
            onChange={(event) => {
              setName(event.target.value);
              // Dropped as soon as the player changes anything, so a stale "that name is taken" does not sit under a new name.
              setErrorMessage(null);
            }}
          />
          <span className="text-xs text-slate-500">Must be unique in the world. (Max {SETTLEMENT_NAME_MAX_LENGTH} characters.)</span>
        </label>

        {errorMessage ? <Alert variant="danger">{errorMessage}</Alert> : null}
      </div>
    </Modal>
  );
}
