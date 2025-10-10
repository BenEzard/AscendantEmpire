import { FaTree } from "react-icons/fa6";
import ToolBar, { type ToolBarGroup } from "../ToolBar";
import { useState } from "react";
import BiomeManagerDialog from "./BiomeManagerDialog";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import type { Biome } from "../../types/map/Biome";
import { Toast, ToastContainer } from "react-bootstrap";

function MapGalleryEditor() {
  const [showBiomeManager, setShowBiomeManager] = useState(false);
  const [showToast, setShowToast] = useState(false);

  const fetchBiomes = () => axios.get<{ biomes: Biome[] }>("https://localhost:7242/biomes/campaign-map").then((res) => res.data.biomes);

  const { data: biomes, error } = useQuery<Biome[], Error>({
    queryKey: ["biomes"],
    queryFn: fetchBiomes,
  });

  if (error) return <p>{error.message}</p>;

  // ToolBar
  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "Palette",
      buttons: [{ key: "biomes", icon: <FaTree />, tooltip: "Biomes", onClick: () => setShowBiomeManager(true) }],
    },
    // {
    //   caption: "Demo",
    //   buttons: [
    //     { key: "center", icon: <FaBullseye />, tooltip: "Center Map" },
    //     { key: "right", icon: <FaArrowsAltH />, tooltip: "Move Right" },
    //   ],
    // },
  ];

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      <BiomeManagerDialog
        show={showBiomeManager}
        biomeList={biomes ?? []}
        onClose={() => setShowBiomeManager(false)}
        onSaveSuccess={() => setShowToast(true)}
      />

      <ToastContainer position="bottom-end" className="p-3">
        <Toast show={showToast} onClose={() => setShowToast(false)} delay={3000} autohide className="me-auto bg-success-subtle border-0">
          <Toast.Body className="bg-success-subtle border-0">Biomes saved successfully!</Toast.Body>
        </Toast>
      </ToastContainer>
    </>
  );
}

export default MapGalleryEditor;
