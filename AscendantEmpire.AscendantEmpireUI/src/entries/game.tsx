import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "@/AppProviders";
import GameUI from "@/features/game/components/GameUI";

// Entry point for the Game page (served at /game/).
createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders>
      {/* No tab strip anymore, so GameUI's default topOffset={0} is correct. */}
      <GameUI />
    </AppProviders>
  </StrictMode>,
);
