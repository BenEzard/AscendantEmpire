import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import { EntitySetupTool } from "@/features/entitysetuptool";

// Entry point for the Entity Setup Tool page (served at /entitysetuptool/).
// A developer-only tool like the Map Editor, so it mounts directly without AuthGate.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <EntitySetupTool />
    </AppProviders>
  </StrictMode>,
);
