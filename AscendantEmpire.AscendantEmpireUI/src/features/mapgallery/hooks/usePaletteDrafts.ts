import { useCallback, useState } from "react";
import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { ActiveDialog } from "@/features/mapgallery/types/MapGalleryEditorTypes";
import { getFirstBiomeType, getFirstGeographyType } from "@/features/mapgallery/tools/paletteDraft";

interface UsePaletteDraftsOptions {
  biomes: Biome[] | undefined;
  biomesLoading: boolean;
  geographies: Geography[] | undefined;
  geographiesLoading: boolean;
  setActiveDialog: (d: ActiveDialog) => void;
}

/**
 * Owns the editor-local draft copies of biomes/geographies plus the current paint
 * selections and manager-dialog selections. `openBiomes`/`openGeographies` seed the
 * draft from the latest query data and open the matching manager dialog.
 */
export function usePaletteDrafts(opts: UsePaletteDraftsOptions) {
  const { biomes, biomesLoading, geographies, geographiesLoading, setActiveDialog } = opts;

  // --- Biomes (draft owned here) ---
  const [biomeDraft, setBiomeDraft] = useState<Biome[]>([]);
  const [biomeMapSelection, setBiomeMapSelection] = useState<number | null>(null);
  const [biomeManagerSelection, setBiomeManagerSelection] = useState<number | null>(null);

  // --- Geographies (draft owned here) ---
  const [geographyDraft, setGeographyDraft] = useState<Geography[]>([]);
  const [geographyMapSelection, setGeographyMapSelection] = useState<number | null>(null);
  const [geographyEditorSelection, setGeographyEditorSelection] = useState<number | null>(null);

  const openBiomes = useCallback(() => {
    if (biomesLoading) return;

    const list = biomes ?? [];
    setBiomeDraft(list);
    setBiomeManagerSelection(getFirstBiomeType(list));
    setActiveDialog("biomes");
  }, [biomesLoading, biomes, setActiveDialog]);

  const openGeographies = useCallback(() => {
    if (geographiesLoading) return;

    const list = geographies ?? [];
    setGeographyDraft(list);
    setGeographyEditorSelection(getFirstGeographyType(list));
    setActiveDialog("geographies");
  }, [geographiesLoading, geographies, setActiveDialog]);

  return {
    biomeDraft,
    setBiomeDraft,
    biomeMapSelection,
    setBiomeMapSelection,
    biomeManagerSelection,
    setBiomeManagerSelection,
    geographyDraft,
    setGeographyDraft,
    geographyMapSelection,
    setGeographyMapSelection,
    geographyEditorSelection,
    setGeographyEditorSelection,
    openBiomes,
    openGeographies,
  };
}
