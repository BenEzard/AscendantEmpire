import { useState } from "react";
import "./App.css";
import { MapGalleryEditor } from "@/features/mapgallery";
import GameUI from "./features/game/components/GameUI";

function App() {
  const [tabKey, setKey] = useState("map-editor");

  return (
    <div className="flex flex-col min-h-screen">
      <div className="flex border-b border-slate-200 bg-white">
        <button
          type="button"
          className={[
            "px-4 py-2 text-sm font-medium",
            tabKey === "map-editor" ? "border-b-2 border-[var(--bs-primary)] text-[var(--bs-primary)]" : "text-slate-600",
          ].join(" ")}
          onClick={() => setKey("map-editor")}>
          Map Gallery
        </button>

        <button
          type="button"
          className={[
            "px-4 py-2 text-sm font-medium",
            tabKey === "game" ? "border-b-2 border-[var(--bs-primary)] text-[var(--bs-primary)]" : "text-slate-600",
          ].join(" ")}
          onClick={() => setKey("game")}>
          Game
        </button>
      </div>

      <div className="flex-1 min-h-0">
        {tabKey === "map-editor" ? <MapGalleryEditor /> : null}
        {tabKey === "game" ? <GameUI /> : null}
      </div>
    </div>
  );
}

export default App;
