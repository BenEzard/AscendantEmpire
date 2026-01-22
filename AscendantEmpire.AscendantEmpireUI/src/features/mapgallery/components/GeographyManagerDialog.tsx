import { Button, Modal, Toast, ToastContainer } from "react-bootstrap";
import Form from "react-bootstrap/Form";
import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import { useMemo, useState } from "react";
import { CiExport } from "react-icons/ci";
import type { Geography } from "../../../types/map/Geography";
import { safeStringify } from "../../../util/json";
import ExportDialog from "../../../components/ExportDialog";
import { useSaveCampaignMapGeographiesMutation } from "../hooks/useCampaignMapGeographies";

interface GeographyManagerProps {
  geographies: Geography[];
  setGeographies: React.Dispatch<React.SetStateAction<Geography[]>>;

  selectedGeographyType: number | null;
  onSelectGeographyType: (id: number | null) => void;

  show: boolean;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

function GeographyManagerDialog({
  geographies,
  setGeographies,
  selectedGeographyType,
  onSelectGeographyType,
  show,
  onClose,
  onSaveSuccess,
}: GeographyManagerProps) {
  // Text Editor Dialog
  const [textEditorTitle, setTextEditorTitle] = useState("Text");
  const [textEditorBody, setTextEditorBody] = useState<string>();
  const [showTextEditor, setShowTextEditor] = useState(false);

  // Toasts
  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast((v) => !v);

  const orderedGeographies = useMemo(() => geographies.slice().sort((a, b) => a.name.localeCompare(b.name)), [geographies]);

  const selectedGeography = useMemo(() => {
    if (selectedGeographyType == null) return null;
    return geographies.find((g) => g.campaignMapGeographyType === selectedGeographyType) ?? null;
  }, [geographies, selectedGeographyType]);

  const handleExportClick = () => {
    setTextEditorTitle("Export Geographies");
    setTextEditorBody(safeStringify(geographies));
    setShowTextEditor(true);
  };

  // When a Geography field changes, update in the master list (draft is owned by parent)
  const handleGeographyFieldChange = <K extends keyof Geography>(field: K, value: Geography[K]) => {
    if (selectedGeographyType == null) return;

    setGeographies((prev) => prev.map((g) => (g.campaignMapGeographyType === selectedGeographyType ? { ...g, [field]: value } : g)));
  };

  const saveGeographyMutation = useSaveCampaignMapGeographiesMutation({
    onSuccess: () => {
      setShowErrorToast(false);
      onClose();
      onSaveSuccess("Geographies saved successfully.");
    },
    onError: (error) => {
      const msg = error instanceof Error ? error.message : "Failed to save geographies.";
      setErrorToastItems([msg]);
      setShowErrorToast(true);
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
                    as="select"
                    htmlSize={Math.min(orderedGeographies.length || 1, 10)}
                    value={selectedGeographyType ?? ""}
                    style={{ flexGrow: 1, minHeight: "200px", maxHeight: "400px", overflowY: "auto" }}
                    onChange={(e) => {
                      const v = e.target.value;
                      onSelectGeographyType(v === "" ? null : Number(v));
                    }}
                    disabled={orderedGeographies.length === 0}>
                    {orderedGeographies.length === 0 ? (
                      <option value="">(no geographies)</option>
                    ) : (
                      orderedGeographies.map((g) => (
                        <option key={g.campaignMapGeographyType} value={g.campaignMapGeographyType}>
                          {g.name}
                        </option>
                      ))
                    )}
                  </Form.Control>

                  <div className="mt-2 d-flex gap-2">
                    <Button variant="primary" className="w-100" onClick={handleExportClick} disabled={geographies.length === 0}>
                      <CiExport aria-hidden="true" strokeWidth={2} /> <span className="ms-1">Export</span>
                    </Button>
                  </div>
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
                      value={selectedGeography?.name ?? ""}
                      disabled={!selectedGeography}
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
                      value={selectedGeography?.relativeElevation ?? ""}
                      disabled={!selectedGeography}
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
                      value={selectedGeography?.movementCost ?? ""}
                      disabled={!selectedGeography}
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
                      value={selectedGeography?.mapEditorIcon ?? ""}
                      disabled={!selectedGeography}
                      onChange={(e) => handleGeographyFieldChange("mapEditorIcon", e.target.value as any)}
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
                      disabled={!selectedGeography}
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
