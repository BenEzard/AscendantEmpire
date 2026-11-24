import { useState } from "react";

interface MapResourceIconProps {
  iconPath: string | null;
  /** Rendered width and height, in px. */
  size: number;
  className?: string;
  title?: string;
}

/**
 * A MapResourceType's image, and nothing at all when it has none or the image fails to load.
 *
 * A type with no icon (e.g. a forest's wood) is said by its Biome, so it draws no marker anywhere — in the map editor as on the game map, which leaves
 * such a resource off the tile altogether and names it in the tile's hover title instead.
 */
export default function MapResourceIcon({ iconPath, size, className, title }: MapResourceIconProps) {
  // Remembered per path, so fixing a broken path in the Type Manager shows the new image.
  const [failedPath, setFailedPath] = useState<string | null>(null);

  if (!iconPath || iconPath === failedPath) {
    return null;
  }

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
