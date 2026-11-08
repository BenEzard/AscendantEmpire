import { useState } from "react";
import { FaFish } from "react-icons/fa6";

interface MapResourceIconProps {
  iconPath: string | null;
  /** Rendered width and height, in px. */
  size: number;
  className?: string;
  title?: string;
}

/**
 * A MapResourceType's image, or the palette's fish when it has none (or the image fails to load).
 *
 * The fish is the editor's stand-in only: a type with no icon (e.g. a forest's wood) shows nothing to Players, but a designer still needs to see where
 * they placed one. The game map therefore never hands this component a null path — it leaves such a resource off the tile altogether and names it in the
 * tile's hover title instead, so the fish cannot reach a Player.
 */
export default function MapResourceIcon({ iconPath, size, className, title }: MapResourceIconProps) {
  // Remembered per path, so fixing a broken path in the Type Manager shows the new image.
  const [failedPath, setFailedPath] = useState<string | null>(null);

  if (iconPath && iconPath !== failedPath) {
    return (
      <img
        src={iconPath}
        alt=""
        title={title}
        width={size}
        height={size}
        className={`object-contain ${className ?? ""}`}
        style={{ width: size, height: size }}
        onError={() => setFailedPath(iconPath)}
      />
    );
  }

  return (
    <span title={title} className={`inline-flex items-center justify-center text-sky-700 ${className ?? ""}`} style={{ width: size, height: size }}>
      <FaFish aria-hidden size={Math.round(size * 0.8)} />
    </span>
  );
}
