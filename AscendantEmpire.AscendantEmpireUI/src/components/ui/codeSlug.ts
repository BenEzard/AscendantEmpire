/**
 * Lowercase a string and replace each non-alphanumeric character with a single hyphen (one per
 * character — runs are NOT collapsed). Does not trim, so it is safe to apply to live manual typing.
 */
export function toCodeChars(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]/g, "-");
}

/**
 * The full code algorithm: {@link toCodeChars} plus trimming of leading/trailing hyphens. Used to
 * auto-derive the code from the source field and when resetting via the pencil.
 */
export function toCodeSlug(source: string): string {
  return toCodeChars(source).replace(/^-+|-+$/g, "");
}
