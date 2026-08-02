import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import PermissionGate from "@/features/auth/components/PermissionGate";
import { System } from "@/features/system";

// Entry point for the System page (served at /system/).
// AuthGate keeps it behind login, and PermissionGate behind the System panel permission since its
// user-administration endpoints (e.g. /auth/users) require exactly that permission.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        <PermissionGate require="SystemPanel">
          <System />
        </PermissionGate>
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
