import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import { System } from "@/features/system";

// Entry point for the System page (served at /system/).
// A developer-only tool like the Map Editor, so it mounts directly without AuthGate.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <System />
    </AppProviders>
  </StrictMode>,
);
