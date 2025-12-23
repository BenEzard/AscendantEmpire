import { Button, Modal, OverlayTrigger, Toast, ToastContainer, Tooltip } from "react-bootstrap";
import Form from "react-bootstrap/Form";
import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import { useState } from "react";
import axios from "axios";
import { CiExport } from "react-icons/ci";
import type { Biome } from "../../../types/map/Biome";
import { safeStringify } from "../../../util/json";
import { useSaveCampaignMapBiomesMutation } from "../hooks/useCampaignMapBiomes";
import { isValidColor, pickTextColorOnBg } from "../../../util/color";
import ExportDialog from "../../../components/ExportDialog";

interface BiomeManagerProps {
  biomes: Biome[];
  setBiomes: React.Dispatch<React.SetStateAction<Biome[]>>;

  selectedBiomeType: number | null;
  onSelectBiomeType: (biomeType: number | null) => void;

  show: boolean;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

function BiomeManagerDialog({ biomes, setBiomes, selectedBiomeType, onSelectBiomeType, show, onClose, onSaveSuccess }: BiomeManagerProps) {
  const selectedBiome = biomes.find((b) => b.biomeType === selectedBiomeType) ?? null;

  // Text Editor Dialog
  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState<string>();
  const [showTextEditor, setShowTextEditor] = useState(false);

  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);

  const mustBeTraversablePaintable = selectedBiome?.isTraversable === false || selectedBiome?.isPaintableOnCampaignMap === false;
  const mustBeTraversablePaintableLand = mustBeTraversablePaintable || selectedBiome?.isWater;

  const orderedBiomes = biomes.slice().sort((a, b) => a.name.localeCompare(b.name));

  const handleBiomeFieldChange = <K extends keyof Biome>(field: K, value: Biome[K]) => {
    if (!selectedBiome) return;

    const originalId = selectedBiome.biomeType;
    const updated = { ...selectedBiome, [field]: value };

    setBiomes((prev) => prev.map((b) => (b.biomeType === originalId ? updated : b)));
  };

  const handleExportClick = () => {
    setTextEditorTitle("Export Biomes");
    setTextEditorBody(safeStringify(biomes));
    setShowTextEditor(true);
  };

  const saveBiomesMutation = useSaveCampaignMapBiomesMutation({
    onSuccess: () => {
      setShowErrorToast(false);

      onClose();
      onSaveSuccess("Biomes saved successfully.");
    },
    onError: (error: any) => {
      let items: string[] = ["An unknown error occurred."];

      if (axios.isAxiosError(error)) {
        const data = error.response?.data;
        if (Array.isArray(data?.errors)) {
          items = data.errors as string[];
        } else if (typeof data === "string") {
          items = [data];
        } else if (data?.message) {
          items = [data.message as string];
        } else if (error.message) {
          items = [error.message as string];
        }
      } else if (error?.message) {
        items = [error.message as string];
      }

      setErrorToastItems(items);
      toggleErrorToast();
      console.error("Save error:", error);
    },
  });

  return (
    <>
      <Modal show={show} onHide={onClose} centered size="xl" contentClassName="thick-border-modal">
        <Modal.Header closeButton>
          <Modal.Title>Biome Manager</Modal.Title>
        </Modal.Header>

        <Modal.Body>
          <Form>
            <Row>
              {/* Column 1: Biome list */}
              <Col sm={3}>
                <Form.Group controlId="biomeList" className="mb-3 h-100 d-flex flex-column">
                  <Form.Label className="fw-bold">Biome List</Form.Label>

                  <Form.Control
                    key={biomes.length}
                    as="select"
                    htmlSize={Math.min(biomes.length, 10)}
                    value={selectedBiome?.biomeType ?? ""}
                    style={{ flexGrow: 1, minHeight: "200px", maxHeight: "400px", overflowY: "auto" }}
                    onChange={(e) => {
                      const selectedType = Number(e.target.value);
                      onSelectBiomeType(Number.isFinite(selectedType) ? selectedType : null);
                    }}>
                    {orderedBiomes.map((b) => (
                      <option key={b.biomeType} value={b.biomeType}>
                        {b.name}
                      </option>
                    ))}
                  </Form.Control>

                  <div className="mt-2 w-100 d-flex gap-2">
                    <OverlayTrigger placement="bottom" overlay={<Tooltip id="tip-export">Export</Tooltip>}>
                      <Button variant="primary" onClick={handleExportClick}>
                        <CiExport aria-hidden="true" strokeWidth={2} />
                      </Button>
                    </OverlayTrigger>
                  </div>
                </Form.Group>
              </Col>

              {/* Column 2: Basic properties */}
              <Col sm={4}>
                <Form.Label className="fw-bold">Selected Biome Properties</Form.Label>

                <Form.Group as={Row} className="mb-1" controlId="Id">
                  <Form.Label column sm={5}>
                    Id
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control type="text" value={selectedBiome?.biomeType ?? ""} disabled readOnly />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Name">
                  <Form.Label column sm={5}>
                    Name
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control type="text" value={selectedBiome?.name ?? ""} onChange={(e) => handleBiomeFieldChange("name", e.target.value)} />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="IsTraversableRow">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="IsTraversable"
                      label="Is Traversable?"
                      checked={selectedBiome?.isTraversable ?? false}
                      onChange={(e) => handleBiomeFieldChange("isTraversable", e.target.checked)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Movement Cost">
                  <Form.Label column sm={5}>
                    Movement Cost
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="number"
                      value={selectedBiome?.movementCost ?? ""}
                      onChange={(e) => handleBiomeFieldChange("movementCost", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="IsWater"
                      disabled={selectedBiome?.canBeElevated}
                      label="Is Water?"
                      checked={selectedBiome?.isWater ?? false}
                      onChange={(e) => handleBiomeFieldChange("isWater", e.target.checked)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="DoesBlockSight"
                      disabled={selectedBiome?.isWater}
                      label="Does Block Sight?"
                      checked={selectedBiome?.doesBlockSight ?? false}
                      onChange={(e) => handleBiomeFieldChange("doesBlockSight", e.target.checked)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="CanBeElevated"
                      disabled={selectedBiome?.isWater}
                      label="Can be Elevated?"
                      checked={selectedBiome?.canBeElevated ?? false}
                      onChange={(e) => handleBiomeFieldChange("canBeElevated", e.target.checked)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="IsPaintable"
                      label="Is Paintable?"
                      checked={selectedBiome?.isPaintableOnCampaignMap ?? true}
                      onChange={(e) => handleBiomeFieldChange("isPaintableOnCampaignMap", e.target.checked)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="HTMLColor">
                  <Form.Label column sm={5}>
                    HTML Color
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="text"
                      value={selectedBiome?.mapEditorHtmlColor ?? ""}
                      onChange={(e) => handleBiomeFieldChange("mapEditorHtmlColor", e.target.value)}
                      style={{
                        backgroundColor: isValidColor(selectedBiome?.mapEditorHtmlColor) ? selectedBiome?.mapEditorHtmlColor! : "white",
                        color: pickTextColorOnBg(isValidColor(selectedBiome?.mapEditorHtmlColor) ? selectedBiome!.mapEditorHtmlColor! : "white"),
                      }}
                    />
                  </Col>
                </Form.Group>
              </Col>

              {/* Column 3 unchanged */}
              <Col sm={5} className="mt-4">
                <Form.Label className="fst-italic">Regenerating Resources</Form.Label>

                <Form.Group as={Row} className="mb-1" controlId="RegenerationRate">
                  <Form.Label column sm={6}>
                    Regeneration Rate
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintable}
                      value={selectedBiome?.regenerationRate ?? ""}
                      onChange={(e) => {
                        const newValue = e.target.value === "" ? null : Number(e.target.value);
                        handleBiomeFieldChange("regenerationRate", newValue as number | null);
                      }}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Forage">
                  <Form.Label column sm={6}>
                    Base Forage
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintableLand}
                      placeholder="n/a"
                      value={selectedBiome?.baseForageValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseForageValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Hunt">
                  <Form.Label column sm={6}>
                    Base Hunt
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintableLand}
                      placeholder="n/a"
                      value={selectedBiome?.baseHuntValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseHuntValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Fish">
                  <Form.Label column sm={6}>
                    Base Fish
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={
                        selectedBiome?.isTraversable === false ||
                        selectedBiome?.isPaintableOnCampaignMap === false ||
                        selectedBiome?.isWater === false
                      }
                      placeholder="n/a"
                      value={selectedBiome?.baseFishValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseFishValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Fodder">
                  <Form.Label column sm={6}>
                    Base Fodder
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintableLand}
                      placeholder="n/a"
                      value={selectedBiome?.baseFodderValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseFodderValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Label className="fst-italic">Passive Material Resources</Form.Label>

                <Form.Group as={Row} className="mb-1" controlId="Bone">
                  <Form.Label column sm={6}>
                    Base Bone
                  </Form.Label>{" "}
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintable}
                      placeholder="n/a"
                      value={selectedBiome?.baseBoneValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseBoneValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Wood">
                  <Form.Label column sm={6}>
                    Base Wood
                  </Form.Label>{" "}
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintableLand}
                      placeholder="n/a"
                      value={selectedBiome?.baseWoodValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseWoodValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Stone">
                  <Form.Label column sm={6}>
                    Base Stone
                  </Form.Label>{" "}
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintableLand}
                      placeholder="n/a"
                      value={selectedBiome?.baseStoneValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseStoneValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Special">
                  <Form.Label column sm={6}>
                    Base Special
                  </Form.Label>
                  <Col sm={3}>
                    <Form.Control
                      type="number"
                      disabled={mustBeTraversablePaintable}
                      placeholder="n/a"
                      value={selectedBiome?.baseSpecialValue ?? ""}
                      onChange={(e) => handleBiomeFieldChange("baseSpecialValue", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>
              </Col>
            </Row>
          </Form>
        </Modal.Body>

        <Modal.Footer>
          <Button variant="secondary" onClick={onClose}>
            Cancel / Close
          </Button>
          <Button variant="success" disabled={saveBiomesMutation.isPending} onClick={() => saveBiomesMutation.mutate(biomes)}>
            {saveBiomesMutation.isPending ? "Saving..." : "Save"}
          </Button>
        </Modal.Footer>
      </Modal>

      <ToastContainer position="bottom-end" className="p-3" style={{ zIndex: 1080 }}>
        <Toast show={showErrorToast} onClose={() => toggleErrorToast()} delay={5000} className="border-0">
          <Toast.Header className="bg-danger text-white border-0 d-flex align-items-center">
            <strong className="me-auto">Save Error</strong>
          </Toast.Header>
          <Toast.Body className="bg-danger-subtle text-danger border-0">
            <ul className="mb-0 ps-3">
              {errorToastItems.map((msg, i) => (
                <li key={i}>{msg}</li>
              ))}
            </ul>
          </Toast.Body>
        </Toast>
      </ToastContainer>

      <ExportDialog show={showTextEditor} title={textEditorTitle} textBody={textEditorBody} onClose={() => setShowTextEditor(false)} />
    </>
  );
}

export default BiomeManagerDialog;
