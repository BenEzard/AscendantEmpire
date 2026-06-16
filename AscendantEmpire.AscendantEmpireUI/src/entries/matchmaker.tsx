import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import MatchMaker from "@/features/matchmaker/components/MatchMaker";

// Entry point for the Match Maker page (served at /matchmaker/).
// AuthGate keeps it behind login/register; once authenticated MatchMaker is shown so the user
// can claim a player slot.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        <MatchMaker />
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
