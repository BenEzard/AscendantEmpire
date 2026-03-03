export type BrushKind = "square" | "diamond" | "vline" | "hline" | "diagDown" | "diagUp";

export const MAX_BRUSH_RADIUS = 5;

const BRUSH_CONSTRAINTS: Record<
  BrushKind,
  {
    // radius 0 => size 1, radius 1 => size 3, etc.
    minRadius: number;
    maxRadiusByDims: (dims: { width: number; height: number }) => number;
  }
> = {
  square: {
    // Single-cell brush: minimum size is 1.
    minRadius: 0,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  diamond: {
    // All other brushes: minimum size is 3.
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  vline: {
    minRadius: 1,
    maxRadiusByDims: ({ height }) => Math.floor((height - 1) / 2),
  },
  hline: {
    minRadius: 1,
    maxRadiusByDims: ({ width }) => Math.floor((width - 1) / 2),
  },
  diagDown: {
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
  diagUp: {
    minRadius: 1,
    maxRadiusByDims: ({ width, height }) => Math.floor((Math.min(width, height) - 1) / 2),
  },
};

export function getMaxRadiusForBrush(kind: BrushKind, dims: { width: number; height: number } | null | undefined): number {
  if (!dims) return MAX_BRUSH_RADIUS;

  const safeDims = {
    width: Math.max(0, dims.width),
    height: Math.max(0, dims.height),
  };

  const maxByDims = BRUSH_CONSTRAINTS[kind].maxRadiusByDims(safeDims);
  return Math.max(0, Math.min(MAX_BRUSH_RADIUS, maxByDims));
}

export function clampBrushRadius(
  kind: BrushKind,
  value: number,
  dims: { width: number; height: number } | null | undefined,
): number {
  const max = getMaxRadiusForBrush(kind, dims);

  const minRequested = BRUSH_CONSTRAINTS[kind].minRadius;
  const min = Math.min(minRequested, max);

  return Math.max(min, Math.min(max, value));
}

export function buildBrushCoordinates(args: {
  origin: { x: number; y: number };
  kind: BrushKind;
  radius: number;
  dims: { width: number; height: number } | null | undefined;
}): { x: number; y: number }[] {
  const { origin, kind, radius, dims } = args;

  if (!dims) return [];

  const inBounds = (x: number, y: number) => x >= 0 && y >= 0 && x < dims.width && y < dims.height;

  const addUnique = (set: Set<string>, coords: { x: number; y: number }[], x: number, y: number) => {
    if (!inBounds(x, y)) return;
    const k = `${x},${y}`;
    if (set.has(k)) return;
    set.add(k);
    coords.push({ x, y });
  };

  const coordinates: { x: number; y: number }[] = [];
  const seen = new Set<string>();

  const r = clampBrushRadius(kind, radius, dims);

  if (kind === "square") {
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) addUnique(seen, coordinates, origin.x + dx, origin.y + dy);
  } else if (kind === "diamond") {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (Math.abs(dx) + Math.abs(dy) <= r) addUnique(seen, coordinates, origin.x + dx, origin.y + dy);
      }
    }
  } else if (kind === "vline") {
    for (let dy = -r; dy <= r; dy++) addUnique(seen, coordinates, origin.x, origin.y + dy);
  } else if (kind === "hline") {
    for (let dx = -r; dx <= r; dx++) addUnique(seen, coordinates, origin.x + dx, origin.y);
  } else if (kind === "diagDown") {
    for (let d = -r; d <= r; d++) addUnique(seen, coordinates, origin.x + d, origin.y + d);
  } else if (kind === "diagUp") {
    for (let d = -r; d <= r; d++) addUnique(seen, coordinates, origin.x + d, origin.y - d);
  }

  return coordinates;
}
