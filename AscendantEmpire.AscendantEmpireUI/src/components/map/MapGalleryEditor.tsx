import { FaMountain, FaTree } from "react-icons/fa6";
import ToolBar, { type ToolBarGroup } from "../ToolBar";
import { useState } from "react";
import BiomeManagerDialog from "./BiomeManagerDialog";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import type { Biome } from "../../types/map/Biome";
import { Toast, ToastContainer } from "react-bootstrap";
import GeographyManagerDialog from "./GeographyManagerDialog";
import type { Geography } from "../../types/map/Geography";

function MapGalleryEditor() {
  const [showBiomeManager, setShowBiomeManager] = useState(false);
  const [showGeographyManager, setShowGeographyManager] = useState(false);

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

  const fetchBiomes = () => axios.get<{ biomes: Biome[] }>("https://localhost:7242/biomes/campaign-map").then((res) => res.data.biomes);

  const fetchGeographies = () =>
    axios.get<{ geographies: Geography[] }>("https://localhost:7242/geography/campaign-map").then((res) => res.data.geographies);

  const { data: biomes, error: biomeError } = useQuery<Biome[], Error>({
    queryKey: ["biomes"],
    queryFn: fetchBiomes,
  });

  const { data: geographies, error: geographyError } = useQuery<Geography[], Error>({
    queryKey: ["geographies"],
    queryFn: fetchGeographies,
  });

  if (biomeError) return <p>{biomeError.message}</p>;
  if (geographyError) return <p>{geographyError.message}</p>;

  // ToolBar
  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "Palette",
      buttons: [
        { key: "biomes", icon: <FaTree size="24" />, tooltip: "Biomes", onClick: () => setShowBiomeManager(true) },
        { key: "geographies", icon: <FaMountain size="24" />, tooltip: "Geographies", onClick: () => setShowGeographyManager(true) },
      ],
    },
  ];

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      <BiomeManagerDialog
        show={showBiomeManager}
        biomeList={biomes ?? []}
        onClose={() => setShowBiomeManager(false)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <GeographyManagerDialog
        show={showGeographyManager}
        geographyList={geographies ?? []}
        onClose={() => setShowGeographyManager(false)}
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
