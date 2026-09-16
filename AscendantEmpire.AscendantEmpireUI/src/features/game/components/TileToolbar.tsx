import Tooltip from "@/components/ui/Tooltip";
import type { IconType } from "react-icons";
import { GiBerryBush, GiDeer, GiFishing, GiHighGrass } from "react-icons/gi";
import type { TileSustenance } from "@/features/map/api/tileSustenanceApi";
import { formatSustenanceAmount } from "./formatSustenanceAmount";

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
}

export interface TileToolbarProps {
  /** The tile the player last clicked, or null when there is nothing to describe (renders nothing). */
  tile: TileToolbarTile | null;
}

/** One sustenance resource as the bar draws it. */
interface SustenanceResourceDisplay {
  key: "forage" | "hunt" | "fish" | "fodder";
  label: string;
  Icon: IconType;
}

/** The four resources in the order the bar always shows them, so a value never shifts position between tiles. */
const SUSTENANCE_RESOURCES: SustenanceResourceDisplay[] = [
  { key: "forage", label: "Forage", Icon: GiBerryBush },
  { key: "hunt", label: "Hunt", Icon: GiDeer },
  { key: "fish", label: "Fish", Icon: GiFishing },
  { key: "fodder", label: "Fodder", Icon: GiHighGrass },
];

/**
 * Describes the tile the player last clicked: its coordinates, its terrain and, when it is in sight, the food it offers them.
 * Docked to the left of the force selection bar, replacing the toast a tile click used to raise — a toast vanished before the
 * player had finished reasoning about the tile, where this stays up until another tile is clicked.
 *
 * Laid out as three lines, most useful first: the terrain ("Grassland, Hills"), then the sustenance, then the coordinates. The
 * sustenance line is simply absent when there is nothing to show, so the bar shrinks to two lines rather than leaving a gap.
 *
 * A presenter, like its neighbours in the dock: it holds no state and makes no decisions.
 */
export default function TileToolbar({ tile }: TileToolbarProps) {
  if (tile == null) {
    return null;
  }

  const sustenance = tile.sustenance?.isObserved ? tile.sustenance : null;

  // A resource the biome does not have is left out altogether rather than shown as a dash, keeping the bar as short as the tile allows. A 0 is still shown:
  // the resource is there, the tile has just been stripped of it.
  const presentResources = sustenance == null ? [] : SUSTENANCE_RESOURCES.filter((resource) => sustenance[resource.key] != null);

  return (
    <div className="tile-toolbar" aria-label="Tile toolbar">
      <span className="tile-toolbar__terrain">{tile.terrainLabel}</span>
      {presentResources.length > 0 && (
        <span className="tile-toolbar__sustenance" aria-label="Sustenance available">
          {presentResources.map(({ key, label, Icon }) => {
            const amount = formatSustenanceAmount(sustenance![key]!);

            return (
              <span key={key} className="tile-toolbar__sustenance-item" aria-label={`${label} ${amount}`}>
                {/* One tooltip over the icon and the number together, so hovering either raises it. The resource name is fixed on the frontend;
                    only the amount comes from the server. The tooltip's own wrapper is what stacks the number under the icon. */}
                <Tooltip content={`${label} ${amount}`} className="tile-toolbar__sustenance-stack">
                  <Icon className="tile-toolbar__sustenance-icon" aria-hidden="true" />
                  <span className="tile-toolbar__sustenance-amount">{amount}</span>
                </Tooltip>
              </span>
            );
          })}
        </span>
      )}
      <span className="tile-toolbar__coordinates">
        ({tile.x}, {tile.y})
      </span>
    </div>
  );
}
