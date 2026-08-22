/**
 * Builds the standard dialog title for a dialog that edits or views one named record:
 * `"<Dialog Name>: <Record Name>"`.
 *
 * The record name is optional at every level — the dialog may have nothing selected (`null`/`undefined`),
 * or the record may exist but still be unnamed (a blank or whitespace-only name, which is the normal state
 * while a new record is being authored). In all of those cases the bare `baseTitle` is returned rather than
 * a title ending in a dangling colon.
 *
 * Keeping this in one place is the point: the separator and the blank-name fallback are the parts that
 * drift when each dialog spells the ternary out for itself.
 *
 * @param baseTitle The dialog's own name, e.g. `"Tag Type Setup"`. Always shown.
 * @param recordName The name of the record currently being edited, if any. Trimmed before use.
 */
export function dialogTitle(baseTitle: string, recordName: string | null | undefined): string {
  const trimmedRecordName = recordName?.trim() ?? "";

  if (trimmedRecordName === "") {
    return baseTitle;
  }

  return `${baseTitle}: ${trimmedRecordName}`;
}
