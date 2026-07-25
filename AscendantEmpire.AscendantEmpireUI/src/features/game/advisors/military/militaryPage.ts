/**
 * The imperative handle a Military Advisor page exposes to its parent dialog, so the dialog's single footer
 * Save button can save whichever page is currently active.
 */
export interface MilitaryPageHandle {
  /** Persists the page's current edits. Resolves when the save completes (or fails, having surfaced its own error). */
  save: () => Promise<void>;
}
