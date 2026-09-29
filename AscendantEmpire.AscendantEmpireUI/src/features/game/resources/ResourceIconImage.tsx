import { useState } from "react";
import { GiCubes } from "react-icons/gi";

interface ResourceIconImageProps {
  /** Root-relative URL from the catalogue, or "" where the row has no icon. */
  iconPath: string;
  /**
   * Rendered width and height, in px.
   *
   * Omit it inside a resource ticker: `ResourceTicker.css` sizes `.resource-ticker__icon img` from the `--rt-size` token, and an inline width or
   * height set here would beat it. Everywhere else — the toolbar button, the Resource Manager header — pass a size, because a bare image has no
   * sizing of its own there.
   */
  size?: number;
  /** Accessible name. Empty (the default) marks the image decorative, which is right wherever a label sits beside it. */
  alt?: string;
  className?: string;
}

/**
 * A Resource's or meta-category's catalogue image, falling back to a generic glyph when it has no icon path or the image fails to load.
 *
 * The path is rendered straight into `src` because these are root-relative URLs served from `public/`, seeded as each row's `IconPath` by
 * `ResourceSeedData` on the API side — not bundled assets with an import to resolve.
 */
export default function ResourceIconImage({ iconPath, size, alt = "", className }: ResourceIconImageProps) {
  // Remembered per path, so a catalogue fix to a broken path shows the new image rather than staying on the fallback.
  const [failedPath, setFailedPath] = useState<string | null>(null);

  if (iconPath !== "" && iconPath !== failedPath) {
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
