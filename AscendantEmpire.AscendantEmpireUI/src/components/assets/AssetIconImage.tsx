import { useState } from "react";
import { GiCubes } from "react-icons/gi";

interface AssetIconImageProps {
  /** Root-relative URL from the catalogue, or "" where the row has no icon. */
  iconPath: string;
  /**
   * Rendered width and height, in px.
   *
   * Omit it inside an asset ticker: `AssetTicker.css` sizes `.asset-ticker__icon img` from the `--rt-size` token, and an inline width or
   * height set here would beat it. Everywhere else — the toolbar button, the Asset Manager header — pass a size, because a bare image has no
   * sizing of its own there.
   */
  size?: number;
  /** Accessible name. Empty (the default) marks the image decorative, which is right wherever a label sits beside it. */
  alt?: string;
  className?: string;
}

/**
 * An Asset's or meta-category's catalogue image, falling back to a generic glyph when it has no icon path or the image fails to load.
 *
 * The path is rendered straight into `src` because these are root-relative URLs served from `public/`, seeded as each row's `IconPath` by
 * `AssetSeedData` on the API side — not bundled assets with an import to resolve.
 *
 * Lives under `components/` rather than with the game feature because the Entity Setup Tool's supply set picker draws Assets as tiles too, and a
 * dev tool reaching into a player-facing feature for a presentational primitive is the wrong dependency direction.
 */
export default function AssetIconImage({ iconPath, size, alt = "", className }: AssetIconImageProps) {
  // Remembered per path, so a catalogue fix to a broken path shows the new image rather than staying on the fallback.
  const [failedPath, setFailedPath] = useState<string | null>(null);

  // Truthiness rather than a comparison with "": an API that predates the field on a given read sends no path at all, and `src={undefined}` renders as an
  // empty box rather than falling back to the glyph.
  if (iconPath && iconPath !== failedPath) {
    return (
      <img
        src={iconPath}
        alt={alt}
        width={size}
        height={size}
        className={className}
        style={size != null ? { width: size, height: size } : undefined}
        draggable={false}
        onError={() => setFailedPath(iconPath)}
      />
    );
  }

  // role/aria-label only when there is a name to carry: an unnamed fallback should stay invisible to assistive technology, the same as alt="".
  return (
    <span
      role={alt === "" ? undefined : "img"}
      aria-label={alt === "" ? undefined : alt}
      aria-hidden={alt === "" ? true : undefined}
      className={className}
      style={size != null ? { width: size, height: size } : undefined}>
      <GiCubes size={size} />
    </span>
  );
}
