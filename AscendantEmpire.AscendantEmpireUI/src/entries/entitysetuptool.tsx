import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import PermissionGate from "@/features/auth/components/PermissionGate";
import { EntitySetupTool } from "@/features/entitysetuptool";

// Entry point for the Entity Setup Tool page (served at /entitysetuptool/).
// A developer tool: AuthGate keeps it behind login, and PermissionGate behind the Entity Setup Tool
// permission, matching the policy on the catalogue-write and seed endpoints it drives.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        <PermissionGate require="EntitySetupTool">
          <EntitySetupTool />
        </PermissionGate>
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
