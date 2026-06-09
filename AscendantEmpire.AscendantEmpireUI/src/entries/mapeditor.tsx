import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import { MapGalleryEditor } from "@/features/mapgallery";

// Entry point for the Map Editor page (served at /mapeditor/).
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <MapGalleryEditor />
    </AppProviders>
  </StrictMode>,
);
