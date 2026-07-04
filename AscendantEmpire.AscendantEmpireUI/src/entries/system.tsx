import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import { System } from "@/features/system";

// Entry point for the System page (served at /system/).
// AuthGate keeps it behind login since its user-administration endpoints (e.g. /auth/users)
// require an authenticated bearer token.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        <System />
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
