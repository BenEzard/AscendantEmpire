import { Button, ButtonGroup, Modal, OverlayTrigger, Toast, ToastContainer, Tooltip } from "react-bootstrap";
import Form from "react-bootstrap/Form";
import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import { useEffect, useState } from "react";
import axios from "axios";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { FaPlusCircle } from "react-icons/fa";
import { CiExport } from "react-icons/ci";
import ExportDialog from "../ExportDialog";
import { safeStringify } from "../../util/json";
import type { Geography } from "../../types/map/Geography";

interface GeographyManagerProps {
  geographyList: Geography[];
  show: boolean;
  onClose: () => void;
  onSaveSuccess: () => void;
}

function GeographyManagerDialog({ geographyList, show, onClose, onSaveSuccess }: GeographyManagerProps) {
  const [geographies, setGeographies] = useState<Geography[]>(geographyList); // local copy so we can modify
  const [selectedGeography, setSelectedGeography] = useState<Geography | null>(null);

  // Text Editor Dialog
  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState<string>();
  const [showTextEditor, setShowTextEditor] = useState(false);

  // Toasts
  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);

  // Default values for Geographies, applied to new Geographies
  const DEFAULTS = {
    relativeElevation: 0,
    movementCost: 1,
    isPaintableOnCampaignMap: true,
  };

  // -----------------------------------------------------------------------------------------------------------------
  // Function that creates a new Geography
  // -----------------------------------------------------------------------------------------------------------------
  const handleAddNewGeography = () => {
    const nextId = Math.max(0, ...geographies.map((b) => b.campaignMapGeographyType)) + 1;

    const newGeography: Geography = {
      campaignMapGeographyType: nextId,
      name: "new",
      relativeElevation: DEFAULTS.relativeElevation,
      movementCost: DEFAULTS.movementCost,
      mapEditorIcon: null,
      isPaintableOnCampaignMap: DEFAULTS.isPaintableOnCampaignMap,
    };

    setGeographies((prev) => [...prev, newGeography]);
    setSelectedGeography(newGeography);
  };

  // Get an order list of the Geography for display in the list
  const orderedGeographies = geographies.slice().sort((a, b) => a.name.localeCompare(b.name));

  // -----------------------------------------------------------------------------------------------------------------
  // When a Geography field changes, update the selected Geography and also the master list
  // -----------------------------------------------------------------------------------------------------------------
  const handleGeographyFieldChange = <K extends keyof Geography>(field: K, value: Geography[K]) => {
    if (!selectedGeography) return;

    const originalId = selectedGeography.campaignMapGeographyType; // track before updating
    const updated = { ...selectedGeography, [field]: value };

    setSelectedGeography(updated);

    setGeographies((prevGeography) => prevGeography.map((b) => (b.campaignMapGeographyType === originalId ? updated : b)));
  };

  const handleExportClick = () => {
    setTextEditorTitle("Export Geographies");
    setTextEditorBody(safeStringify(geographies));
    setShowTextEditor(true);
  };

  // -----------------------------------------------------------------------------------------------------------------
  // Load geographyList only once when dialog first opens
  // -----------------------------------------------------------------------------------------------------------------
  useEffect(() => {
    if (show) {
      console.table(geographyList.map((g) => ({ id: g.campaignMapGeographyType, type: typeof g.campaignMapGeographyType })));

      setGeographies(geographyList);

      // Sort to ensure consistent order
      const first = geographyList.slice().sort((a, b) => a.name.localeCompare(b.name))[0] ?? null;
      setSelectedGeography(first);
    }
  }, [show]); // only run when dialog is opened

  const queryClient = useQueryClient();

  const saveGeographyMutation = useMutation({
    mutationFn: async (geographiesToSave: Geography[]) => {
      const response = await axios.put("https://localhost:7242/geography/campaign-map", geographiesToSave, {
        headers: { "Content-Type": "application/json" },
      });

      return response.data; // response.data contains parsed JSON body
    },
    onSuccess: () => {
      // make sure the error toast is hidden
      setShowErrorToast(false);

      // Invalidate or update the cache if you have a geography query elsewhere
      queryClient.invalidateQueries({ queryKey: ["geographies"] });

      onClose();
      onSaveSuccess(); // notify parent
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
      <Modal show={show} onHide={onClose} centered size="lg" contentClassName="thick-border-modal">
        <Modal.Header closeButton>
          <Modal.Title>Geography Manager</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <Form>
            <Row>
              {/* Column 1: Geography list */}
              <Col sm={3}>
                <Form.Group controlId="geographyList" className="mb-3 h-100 d-flex flex-column">
                  <Form.Label className="fw-bold">Geography List</Form.Label>
                  <Form.Control
                    key={geographies.length} // forces refresh when list changes
                    as="select"
                    htmlSize={Math.min(geographies.length, 10)}
                    value={selectedGeography?.campaignMapGeographyType ?? ""}
                    style={{ flexGrow: 1, minHeight: "200px", maxHeight: "400px", overflowY: "auto" }}
                    onChange={(e) => {
                      const selectedType = Number(e.target.value);
                      const geography = geographies.find((b) => b.campaignMapGeographyType === selectedType) ?? null;
                      setSelectedGeography(geography);
                    }}>
                    {orderedGeographies.map((b) => (
                      <option key={b.campaignMapGeographyType} value={b.campaignMapGeographyType}>
                        {b.name}
                      </option>
                    ))}
                  </Form.Control>

                  <ButtonGroup className="mt-2 w-100">
                    <OverlayTrigger placement="bottom" overlay={<Tooltip id="tip-add">Add New</Tooltip>}>
                      <Button variant="primary" onClick={handleAddNewGeography}>
                        <FaPlusCircle aria-hidden="true" />
                      </Button>
                    </OverlayTrigger>
                    <OverlayTrigger placement="bottom" overlay={<Tooltip id="tip-add">Export</Tooltip>}>
                      <Button variant="primary" onClick={handleExportClick}>
                        <CiExport aria-hidden="true" strokeWidth={2} />
                      </Button>
                    </OverlayTrigger>
                  </ButtonGroup>
                </Form.Group>
              </Col>

              {/* Column 2: Basic properties */}
              <Col sm={6}>
                <Form.Label className="fw-bold">Selected Geography Properties</Form.Label>

                <Form.Group as={Row} className="mb-1" controlId="Id">
                  <Form.Label column sm={5}>
                    Id
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control type="text" value={selectedGeography?.campaignMapGeographyType ?? ""} disabled readOnly />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="Name">
                  <Form.Label column sm={5}>
                    Name
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="text"
                      placeholder=""
                      value={selectedGeography?.name ?? ""}
                      onChange={(e) => handleGeographyFieldChange("name", e.target.value)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="RelativeElevation">
                  <Form.Label column sm={5}>
                    Relative Elevation
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="number"
                      placeholder=""
                      value={selectedGeography?.relativeElevation ?? ""}
                      onChange={(e) => handleGeographyFieldChange("relativeElevation", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="MovementCost">
                  <Form.Label column sm={5}>
                    Movement Cost
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="number"
                      placeholder=""
                      value={selectedGeography?.movementCost ?? ""}
                      onChange={(e) => handleGeographyFieldChange("movementCost", Number(e.target.value))}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1" controlId="MapEditorIcon">
                  <Form.Label column sm={5}>
                    Map Editor Icon
                  </Form.Label>
                  <Col sm={5}>
                    <Form.Control
                      type="text"
                      placeholder=""
                      value={selectedGeography?.mapEditorIcon ?? ""}
                      onChange={(e) => handleGeographyFieldChange("mapEditorIcon", e.target.value)}
                    />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-1">
                  <Col sm={12}>
                    <Form.Check
                      type="checkbox"
                      id="IsPaintableOnCampaignMap"
                      label="Is Paintable on Campaign Map?"
                      checked={selectedGeography?.isPaintableOnCampaignMap ?? false}
                      onChange={(e) => handleGeographyFieldChange("isPaintableOnCampaignMap", e.target.checked)}
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
          <Button variant="success" disabled={saveGeographyMutation.isPending} onClick={() => saveGeographyMutation.mutate(geographies)}>
            {saveGeographyMutation.isPending ? "Saving..." : "Save"}
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

export default GeographyManagerDialog;
