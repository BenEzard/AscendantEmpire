import { useState } from "react";
import Tab from "react-bootstrap/Tab";
import Tabs from "react-bootstrap/Tabs";
import "./App.css";
import { MapGalleryEditor } from "@/features/mapgallery";
import GameUI from "./features/game/components/GameUI";

function App() {
  const [tabKey, setKey] = useState("map-editor");

  return (
    <Tabs id="controlled-tab-example" activeKey={tabKey} onSelect={(k: string | null) => setKey(k ?? "home")} className="mb-0">
      <Tab eventKey="map-editor" title="Map Gallery">
        <MapGalleryEditor />
      </Tab>
      <Tab eventKey="game" title="Game">
        <GameUI />
      </Tab>
    </Tabs>
  );
}

export default App;
