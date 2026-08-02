import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import PermissionGate from "@/features/auth/components/PermissionGate";
import { MapGalleryEditor } from "@/features/mapgallery";

// Entry point for the Map Editor page (served at /mapeditor/).
// AuthGate keeps it behind login, and PermissionGate behind the Map Editor permission — every endpoint
// this page mutates is gated on the same permission server-side, so without it the page would be a wall
// of 403s.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        <PermissionGate require="MapEditor">
          <MapGalleryEditor />
        </PermissionGate>
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
