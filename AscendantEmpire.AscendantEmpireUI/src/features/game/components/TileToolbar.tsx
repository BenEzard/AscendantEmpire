import Tooltip from "@/components/ui/Tooltip";
import { GiHighGrass } from "react-icons/gi";
import appleIconUrl from "@/assets/icons/apple_20px.png";
import MapResourceIcon from "@/components/map/MapResourceIcon";
import type { TileSustenance } from "@/features/map/api/tileSustenanceApi";
import type { TileMapResourceMarker } from "@/features/map/components/TileTypes";
import { describeMapResource } from "@/features/map/util/mapResourceMarkers";
import { formatCompactAmount } from "@/util/number";
import { SUPPORT_LEVEL_LABELS, tileSupportLevelClass } from "./tileSupportLevelClass";

export interface TileToolbarTile {
  x: number;
  y: number;

  /**
   * The tile's terrain, already named ("Grassland, Hills", or "Unknown" for ground the player has never seen). Resolved by
   * the caller, which holds the biome and geography lookups — the bar only draws what it is given.
   */
  terrainLabel: string;

  /**
   * What the player could feed on here, or null when there is nothing to show — the tile is out of sight, or the values have
   * not arrived yet. Nothing is drawn for a tile that is not observed, even if the caller passes one.
   */
  sustenance?: TileSustenance | null;

  /**
   * The MapResources the player has discovered here, in slot order, or omitted for a tile with none.
   *
   * Unlike the sustenance above, these are drawn whether or not the tile is watched: a discovered patch stays on the player's map. Each carries its own
   * estimate, which is absent off live sight, so the bar never has to ask about sight itself.
   */
  mapResources?: readonly TileMapResourceMarker[];
}

export interface TileToolbarProps {
  /** The tile the player last clicked, or null when there is nothing to describe (renders nothing). */
  tile: TileToolbarTile | null;
}

/** One of the resources that feeds the sustenance total, as the apple's tooltip breaks it back down. */
interface SustenanceResourceDisplay {
  key: "forage" | "hunt" | "fish";
  label: string;
}

/**
 * Everything the player themselves can eat, in the order the apple's tooltip always lists them, so a resource never shifts position between tiles.
 *
 * The bar shows only the sum of these, under a single apple; this list is what the apple's tooltip breaks that sum back down into. MapResources are not
 * among them: a patch holds a stock the player extracts from, not a rate they feed at, so adding one to the apple's total would add unlike things. They
 * get their own items further along the bar instead.
 */
const SUSTENANCE_RESOURCES: SustenanceResourceDisplay[] = [
  { key: "forage", label: "Forage" },
  { key: "hunt", label: "Hunt" },
  { key: "fish", label: "Fish" },
];

/**
 * Describes the tile the player last clicked: its coordinates, its terrain and, when it is in sight, the food it offers them.
 * Docked to the left of the force selection bar, replacing the toast a tile click used to raise — a toast vanished before the
 * player had finished reasoning about the tile, where this stays up until another tile is clicked.
 *
 * Laid out as lines, most useful first: the terrain ("Grassland, Hills"), then the sustenance, then the MapResources found here, then the coordinates. A
 * line with nothing to show is simply absent, so the bar shrinks rather than leaving a gap.
 *
 * The sustenance line carries at most two figures: the apple, which is everything the player can eat on this tile added together, and fodder, which is kept
 * apart from it because it feeds their animals rather than them. Hovering the apple names the resources behind its total in a plain tooltip, the same kind
 * fodder carries: "Forage (400) + Hunt (12)".
 *
 * The MapResource line carries one item per resource discovered here, in slot order: its icon with this player's estimate beneath it, and its name in place
 * of the icon for a type the biome already says. The estimate is absent whenever the server gave none — the tile is out of sight, or the resource holds its
 * quantity in layers to survey — rather than shown as a 0. Unlike the sustenance above, the line is drawn whether or not the tile is watched: a discovered
 * patch stays on the player's map.
 *
 * The background says whether the tile can feed the player's forces standing on it: green for two turns or more, yellow for one, red when it can't feed them
 * all, and dark red when nothing is left — dark red shows even with no forces on the tile. No level, no colour.
 *
 * A presenter, like its neighbours in the dock: it holds no state and makes no decisions.
 */
export default function TileToolbar({ tile }: TileToolbarProps) {
  if (tile == null) {
    return null;
  }

  const sustenance = tile.sustenance?.isObserved ? tile.sustenance : null;

  // A resource the biome does not have is left out of the breakdown altogether rather than shown as a dash, keeping the tooltip as short as the tile allows.
  // A 0 is still listed: the resource is there, the tile has just been stripped of it.
  const presentResources = sustenance == null ? [] : SUSTENANCE_RESOURCES.filter((resource) => sustenance[resource.key] != null);

  // Summed raw and formatted once, rather than by adding up the already-formatted parts: three lots of 0.9 are 2 of food between them, not 0.
  const sustenanceTotal = presentResources.reduce((runningTotal, resource) => runningTotal + sustenance![resource.key]!, 0);
  const sustenanceAmount = formatCompactAmount(sustenanceTotal);

  // What the total is made of, named and added up the way the player would say it: "Forage (400) + Hunt (12)". Serves as the apple's tooltip, and is folded
  // into the item's label below for screen readers, which cannot hover it.
  const sustenanceBreakdownLabel = presentResources.map(({ key, label }) => `${label} (${formatCompactAmount(sustenance![key]!)})`).join(" + ");

  // Fodder is not food for the player — it feeds their animals — so it keeps its own icon beside the apple rather than folding into the total.
  const fodderAmount = sustenance?.fodder == null ? null : formatCompactAmount(sustenance.fodder);

  // Not gated on sight, unlike everything above: a discovered patch stays on the player's map, and its own estimate is already absent off live sight.
  const mapResources = tile.mapResources ?? [];

  // Whether the tile can feed the forces on it colours the whole bar. Only honoured for an observed tile, like the amounts: a level discloses what is left.
  const supportLevel = sustenance?.supportLevel ?? null;
  const className = ["tile-toolbar", tileSupportLevelClass("tile-toolbar", supportLevel)].filter(Boolean).join(" ");

  return (
    // The level is named for screen readers through aria-description rather than a native title: a title would raise a browser tooltip on top of the
    // resource tooltips inside the bar.
    <div className={className} aria-label="Tile toolbar" aria-description={supportLevel == null ? undefined : SUPPORT_LEVEL_LABELS[supportLevel]}>
      <span className="tile-toolbar__terrain">{tile.terrainLabel}</span>
      {/* One line for every figure the tile offers, so the resources sit beside the food rather than under it. Absent when the tile offers none. */}
      {(presentResources.length > 0 || fodderAmount != null || mapResources.length > 0) && (
        <span className="tile-toolbar__amounts">
          {(presentResources.length > 0 || fodderAmount != null) && (
            <span className="tile-toolbar__sustenance" aria-label="Sustenance available">
              {presentResources.length > 0 && (
                <span className="tile-toolbar__sustenance-item" aria-label={`Sustenance ${sustenanceAmount}: ${sustenanceBreakdownLabel}`}>
                  {/* One tooltip over the icon and its total together, exactly as fodder has: hovering either names the resources the total came from. */}
                  <Tooltip content={sustenanceBreakdownLabel} className="tile-toolbar__sustenance-stack">
                    {/* A bitmap rather than a react-icons glyph, so unlike its neighbour it keeps its own colours instead of taking the bar's text colour. */}
                    <img src={appleIconUrl} className="tile-toolbar__sustenance-icon" alt="" />
                    <span className="tile-toolbar__sustenance-amount">{sustenanceAmount}</span>
                  </Tooltip>
                </span>
              )}
              {fodderAmount != null && (
                <span className="tile-toolbar__sustenance-item" aria-label={`Fodder ${fodderAmount}`}>
                  {/* Fodder is a single resource with nothing to break down, so its tooltip stays the plain name-and-amount the bar has always shown. */}
                  <Tooltip content={`Fodder ${fodderAmount}`} className="tile-toolbar__sustenance-stack">
                    <GiHighGrass className="tile-toolbar__sustenance-icon" aria-hidden="true" />
                    <span className="tile-toolbar__sustenance-amount">{fodderAmount}</span>
                  </Tooltip>
                </span>
              )}
            </span>
          )}
          {mapResources.length > 0 && (
            <span className="tile-toolbar__map-resources" aria-label="MapResources found here">
              {mapResources.map((marker) => {
                // Named with its estimate for the tooltip, and for screen readers, which cannot hover one.
                const label = describeMapResource(marker);

                return (
                  <span key={marker.slot} className="tile-toolbar__map-resource-item" aria-label={label}>
                    <Tooltip content={label} className="tile-toolbar__sustenance-stack">
                      {marker.iconPath == null ? (
                        // A type the biome already says carries no icon of its own, so the bar names it rather than leaving the player a gap to interpret.
                        <span className="tile-toolbar__map-resource-name">{marker.name}</span>
                      ) : (
                        <MapResourceIcon iconPath={marker.iconPath} size={21} />
                      )}
                      {/* Absent, never a 0 or a dash: a patch whose size the player cannot make out is not a patch that has been stripped bare. */}
                      {marker.estimatedQuantity != null && (
                        <span className="tile-toolbar__sustenance-amount">{formatCompactAmount(marker.estimatedQuantity)}</span>
                      )}
                    </Tooltip>
                  </span>
                );
              })}
            </span>
          )}
        </span>
      )}
      <span className="tile-toolbar__coordinates">
        ({tile.x}, {tile.y})
      </span>
    </div>
  );
}
