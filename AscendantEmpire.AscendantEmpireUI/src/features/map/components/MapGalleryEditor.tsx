import { FaMountain, FaRegFolderOpen, FaTree } from "react-icons/fa6";
import { FaPlusCircle } from "react-icons/fa";
import ToolBar, { type ToolBarGroup } from "@/components/Toolbar";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import type { Biome } from "../../../types/map/Biome";
import { Toast, ToastContainer } from "react-bootstrap";
import type { Geography } from "../../../types/map/Geography";
import { VscNewFile } from "react-icons/vsc";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog from "./OpenCampaignMapDialog";
import { useCampaignMapBiomesQuery } from "../hooks/useCampaignMapBiomes";
import BiomeManagerDialog from "./BiomeManagerDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";

function MapGalleryEditor() {
  type ActiveDialog = "biomes" | "geographies" | "newCampaignMap" | "openCampaignMap" | null;
  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  // Biome draft state (owned by parent so "Add" can live here, not inside the dialog)
  const [biomeDraft, setBiomeDraft] = useState<Biome[]>([]);
  const [selectedBiomeType, setSelectedBiomeType] = useState<number | null>(null);

  // Toast
  const [toast, setToast] = useState<{
    show: boolean;
    message: string;
    variant: "success" | "danger" | "warning" | "info";
  }>({
    show: false,
    message: "",
    variant: "success",
  });

  const openToast = (message: string, variant: "success" | "danger" | "warning" | "info" = "success") => setToast({ show: true, message, variant });

  const closeToast = () => setToast((t) => ({ ...t, show: false }));

  const fetchGeographies = () =>
    axios.get<{ geographies: Geography[] }>("https://localhost:7242/geography/campaign-map").then((res) => res.data.geographies);

  const { data: biomes, error: biomeError, isLoading: biomesLoading } = useCampaignMapBiomesQuery();

  const { data: geographies, error: geographyError } = useQuery<Geography[], Error>({
    queryKey: ["geographies"],
    queryFn: fetchGeographies,
  });

  if (biomeError) return <p>{biomeError.message}</p>;
  if (geographyError) return <p>{geographyError.message}</p>;

  const getFirstBiomeType = (list: Biome[]) => list.slice().sort((a, b) => a.name.localeCompare(b.name))[0]?.biomeType ?? null;

  const prepareBiomeDraft = () => {
    const list = biomes ?? [];
    setBiomeDraft(list);
    setSelectedBiomeType(getFirstBiomeType(list));
  };

  // Default values for Biomes, applied to new Biomes
  const DEFAULTS = {
    regenerationRate: 1.1,
    movementCost: 1,
    isTraversable: true,
    isWater: false,
    isPaintableOnCampaignMap: true,
    doesBlockSight: false,
  };

  const createNewBiome = (nextId: number): Biome => ({
    biomeType: nextId,
    name: "new",
    isTraversable: DEFAULTS.isTraversable,
    movementCost: DEFAULTS.movementCost,
    isWater: DEFAULTS.isWater,
    doesBlockSight: DEFAULTS.doesBlockSight,
    canBeElevated: false,
    isPaintableOnCampaignMap: DEFAULTS.isPaintableOnCampaignMap,
    mapEditorHtmlColor: "",
    regenerationRate: DEFAULTS.regenerationRate,
    baseForageValue: null,
    baseHuntValue: null,
    baseFishValue: null,
    baseFodderValue: null,
    baseBoneValue: null,
    baseWoodValue: null,
    baseStoneValue: null,
    baseSpecialValue: null,
  });

  const addNewBiomeAndOpen = () => {
    // Ensure draft is initialized from latest query data if user hasn't opened the dialog yet
    const baseList = activeDialog === "biomes" ? biomeDraft : biomes ?? [];
    const nextId = Math.max(0, ...baseList.map((b) => b.biomeType)) + 1;

    const newBiome = createNewBiome(nextId);

    setBiomeDraft([...baseList, newBiome]);
    setSelectedBiomeType(newBiome.biomeType);
    setActiveDialog("biomes");
  };

  // ToolBar
  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "File",
      buttons: [
        { key: "new", icon: <VscNewFile />, tooltip: "New Campaign Map", onClick: () => setActiveDialog("newCampaignMap") },
        { key: "open", icon: <FaRegFolderOpen />, tooltip: "Open Campaign Map", onClick: () => setActiveDialog("openCampaignMap") },
      ],
    },
    {
      caption: "Palette",
      buttons: [
        {
          key: "biomes",
          icon: <FaTree />,
          tooltip: "Biomes",
          onClick: () => {
            prepareBiomeDraft();
            setActiveDialog("biomes");
          },
        },
        {
          key: "addBiome",
          icon: <FaPlusCircle />,
          tooltip: biomesLoading ? "Loading Biomes..." : "Add Biome",
          onClick: () => {
            if (biomesLoading) return;
            addNewBiomeAndOpen();
          },
        },
        { key: "geographies", icon: <FaMountain />, tooltip: "Geographies", onClick: () => setActiveDialog("geographies") },
      ],
    },
  ];

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      <BiomeManagerDialog
        show={activeDialog === "biomes"}
        biomes={biomeDraft}
        selectedBiomeType={selectedBiomeType}
        onSelectBiomeType={setSelectedBiomeType}
        setBiomes={setBiomeDraft}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <GeographyManagerDialog
        show={activeDialog === "geographies"}
        geographyList={geographies ?? []}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <NewCampaignMapDialog
        show={activeDialog === "newCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <OpenCampaignMapDialog
        show={activeDialog === "openCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <ToastContainer position="bottom-end" className="p-3">
        <Toast show={toast.show} onClose={closeToast} autohide delay={3000} className={`border-0 bg-${toast.variant}-subtle`}>
          <Toast.Body className={`border-0 ${toast.variant === "warning" ? "" : `text-${toast.variant}`}`}>{toast.message}</Toast.Body>
        </Toast>
      </ToastContainer>
    </>
  );
}

export default MapGalleryEditor;
