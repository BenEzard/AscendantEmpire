import { useEffect, useRef, useState } from "react";
import "./App.css";
import { MapGalleryEditor } from "@/features/mapgallery";
import GameUI from "./features/game/components/GameUI";

function App() {
  const [tabKey, setKey] = useState("map-editor");
  const tabStripRef = useRef<HTMLDivElement>(null);
  const [tabStripBottomPx, setTabStripBottomPx] = useState(0);

  useEffect(() => {
    const compute = () => {
      const el = tabStripRef.current;
      if (!el) return;
      setTabStripBottomPx(Math.round(el.getBoundingClientRect().bottom));
    };

    compute();
    window.addEventListener("resize", compute);

    const el = tabStripRef.current;
    if (el && "ResizeObserver" in window) {
      const ro = new ResizeObserver(compute);
      ro.observe(el);
      return () => {
        ro.disconnect();
        window.removeEventListener("resize", compute);
      };
    }

    return () => window.removeEventListener("resize", compute);
  }, []);

  return (
    <div className="flex flex-col min-h-screen">
      <div ref={tabStripRef} className="fixed top-0 left-0 right-0 z-[1080] flex border-b border-slate-200 bg-white">
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

      {/* Spacer to push content below the fixed tab strip */}
      <div style={{ height: tabStripBottomPx }} />

      <div className="flex-1 min-h-0">
        <div className={tabKey === "map-editor" ? "block h-full" : "hidden"} aria-hidden={tabKey !== "map-editor"}>
          <MapGalleryEditor />
        </div>

        <div className={tabKey === "game" ? "block h-full" : "hidden"} aria-hidden={tabKey !== "game"}>
          <GameUI topOffset={tabStripBottomPx} />
        </div>
      </div>
    </div>
  );
}

export default App;
