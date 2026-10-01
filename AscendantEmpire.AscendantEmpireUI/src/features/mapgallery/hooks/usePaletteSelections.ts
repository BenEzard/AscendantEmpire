import { useCallback, useState } from "react";
import type { ActiveDialog } from "@/features/mapgallery/types/MapGalleryEditorTypes";

interface UsePaletteSelectionsOptions {
  biomesLoading: boolean;
  geographiesLoading: boolean;
  setActiveDialog: (d: ActiveDialog) => void;
}

/**
 * Owns the editor's current biome and geography paint selections, plus the two openers behind the palette buttons.
 * Both catalogues are edited in the Entity Setup Tool rather than here, so opening either one just shows the
 * read-only reference table, which reads the query directly and needs no draft of its own.
 */
export function usePaletteSelections(opts: UsePaletteSelectionsOptions) {
  const { biomesLoading, geographiesLoading, setActiveDialog } = opts;

  const [biomeMapSelection, setBiomeMapSelection] = useState<number | null>(null);
  const [geographyMapSelection, setGeographyMapSelection] = useState<number | null>(null);

  const openBiomes = useCallback(() => {
    if (biomesLoading) return;

    setActiveDialog("biomes");
  }, [biomesLoading, setActiveDialog]);

  const openGeographies = useCallback(() => {
    if (geographiesLoading) return;

    setActiveDialog("geographies");
  }, [geographiesLoading, setActiveDialog]);

  return {
    biomeMapSelection,
    setBiomeMapSelection,
    geographyMapSelection,
    setGeographyMapSelection,
    openBiomes,
    openGeographies,
  };
}
