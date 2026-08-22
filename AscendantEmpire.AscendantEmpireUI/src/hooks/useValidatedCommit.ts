// useValidatedCommit — the shared "Save was pressed" policy for the setup/manager dialogs.
//
// Every one of these dialogs wants the same three things, and each had grown its own copy:
//
//   1. Validation messages stay hidden until the first Save, so a freshly opened form isn't
//      pre-littered with "required" errors; from then on they update live as fields are filled in.
//   2. Save is always clickable rather than disabled — a greyed-out button leaves the user guessing
//      why — so pressing it is what reveals the problems.
//   3. Only part of the form is on screen at a time (a page in a PagedDialog, a row in a list), so if
//      the offending field is somewhere the user can't see, Save looks like it silently did nothing.
//      The failing attempt therefore has to move them to the problem.
//
// The hook owns that policy; the caller owns *what* is invalid and *where* it lives. Deliberately
// agnostic about the axis being navigated — `locate` is just a callback, so one caller switches the
// visible page and another changes the selected row.

import { useEffect, useState } from "react";

export interface UseValidatedCommitOptions {
  /** Whether the dialog is open. The revealed-errors flag resets each time it opens. */
  open: boolean;
  /** Whether there is nothing left to fix. The caller computes this however it likes. */
  isValid: boolean;
  /**
   * Moves the user to whatever is holding the problem — the page carrying the error, the first
   * offending row, and so on. Called only when an attempt fails, and only after `submitted` is set,
   * so the inline messages are already revealed by the time the user arrives.
   */
  locate?: () => void;
  /** Runs only once everything passes. This is where the actual commit happens. */
  onValid: () => void;
}

export interface ValidatedCommit {
  /**
   * Whether a commit has been attempted since the dialog opened. Gate the inline error messages on
   * this, e.g. `const shownErrors = submitted ? fieldErrors : {}`.
   */
  submitted: boolean;
  /** Wire this to the Save button (and to PagedDialog's `onCommit`). */
  attemptCommit: () => void;
}

export function useValidatedCommit({ open, isValid, locate, onValid }: UseValidatedCommitOptions): ValidatedCommit {
  const [submitted, setSubmitted] = useState(false);

  // Start quiet again on each open, so a previous session's errors never greet a fresh form.
  useEffect(() => {
    if (open) {
      setSubmitted(false);
    }
  }, [open]);

  // Not memoised on purpose: it closes over this render's `isValid` / `onValid`, which is exactly the
  // behaviour wanted for an event handler passed straight to a button.
  const attemptCommit = () => {
    // Reveal validation now that a save was attempted, whatever the outcome.
    setSubmitted(true);

    if (!isValid) {
      locate?.();
      return;
    }

    onValid();
  };

  return { submitted, attemptCommit };
}
