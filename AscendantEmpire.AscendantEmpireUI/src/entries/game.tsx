import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import AuthGate from "@/features/auth/components/AuthGate";
import GameUI from "@/features/game/components/GameUI";

// Entry point for the Game page (served at /game/).
// AuthGate keeps the game behind login; once authenticated GameUI is shown.
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      <AuthGate>
        {/* No tab strip anymore, so GameUI's default topOffset={0} is correct. */}
        <GameUI />
      </AuthGate>
    </AppProviders>
  </StrictMode>,
);
