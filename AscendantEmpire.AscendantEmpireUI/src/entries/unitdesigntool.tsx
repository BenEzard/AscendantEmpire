import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import { UnitDesignerTool } from "@/features/unitdesigntool";

// Entry point for the Unit Designer page (served at /unitdesigntool/).
// A developer-only tool like the Map Editor, so it mounts directly without AuthGate.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <UnitDesignerTool />
    </AppProviders>
  </StrictMode>,
);
