/** Shown on the pages that pick Resources when none exist yet, so an empty picker and a disabled "+" are explained. */
export default function NoResourcesHint() {
  return (
    <p className="mb-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
      No Resources exist yet. Add them in the Entity Setup Tool before giving a MapResource its Resources.
    </p>
  );
}
