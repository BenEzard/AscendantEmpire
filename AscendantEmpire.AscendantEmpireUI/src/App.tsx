import { useState } from "react";
import Tab from "react-bootstrap/Tab";
import Tabs from "react-bootstrap/Tabs";
import "./App.css";
import MapGalleryEditor from "./components/map/MapGalleryEditor";

function App() {
  const [tabKey, setKey] = useState("home");

  return (
    <Tabs id="controlled-tab-example" activeKey={tabKey} onSelect={(k: string | null) => setKey(k ?? "home")} className="mb-0">
      <Tab eventKey="home" title="Map Gallery">
        <MapGalleryEditor />
      </Tab>
    </Tabs>
  );
}

export default App;
