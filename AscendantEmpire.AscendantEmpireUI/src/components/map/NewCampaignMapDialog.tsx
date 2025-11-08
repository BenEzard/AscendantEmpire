import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Modal, Button, Form, Row, Col, ToastContainer, Toast } from "react-bootstrap";
import axios from "axios";

// --- String-based engine names ---
export const engineOptions = ["BlankMap", "PerlinV1"] as const;
export type MapCreationEngine = (typeof engineOptions)[number];

export interface NewCampaignMapValues {
  id: number | null; // null for new records
  mapName: string;
  description: string;
  width: number;
  height: number;
  creationEngine: number;
  properties: string; // JSON or free text; up to you
}

export interface AddCampaignMapRequest {
  id?: number | null /** null/undefined for create; number for update */;
  mapName: string;
  description: string;
  width: number;
  height: number;
  creationEngine: number;
  parameters?: Record<string, string>;
}

export interface NewCampaignMapDialogProps {
  show: boolean;
  initialValues?: Partial<NewCampaignMapValues>;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

const emptyValues: NewCampaignMapValues = {
  id: null,
  mapName: "",
  description: "",
  width: 10,
  height: 10,
  creationEngine: 0,
  properties: "{}",
};

export default function NewCampaignMapDialog({ show, initialValues, onClose, onSaveSuccess }: NewCampaignMapDialogProps) {
  const [campaignMap, setCampaignMap] = useState<NewCampaignMapValues>({ ...emptyValues });
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Toasts
  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);

  useEffect(() => {
    if (show) {
      setCampaignMap({ ...emptyValues, ...(initialValues || {}) } as NewCampaignMapValues);
      setErrors({});
      setSaving(false);
      setShowErrorToast(false);
    }
  }, [show, initialValues]);

  function set<K extends keyof NewCampaignMapValues>(key: K, val: NewCampaignMapValues[K]) {
    setCampaignMap((s) => ({ ...s, [key]: val }));
  }

  const MIN_WIDTH = 10;
  const MIN_HEIGHT = 10;
  const MAX_WIDTH = 600;
  const MAX_HEIGHT = 375;

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!campaignMap.mapName?.trim()) e.mapName = "Map name is required.";
    if (!campaignMap.description?.trim()) e.desciption = "Description is required.";
    if (campaignMap.width < MIN_WIDTH || campaignMap.width > MAX_WIDTH) {
      e.width = `Width must be between ${MIN_WIDTH} and ${MAX_WIDTH}.`;
    }
    if (campaignMap.height < MIN_HEIGHT || campaignMap.height > MAX_HEIGHT) {
      e.height = `Height must be between ${MIN_HEIGHT} and ${MAX_HEIGHT}.`;
    }

    // If you expect JSON in properties, validate softly
    try {
      if (campaignMap.properties?.trim()) {
        JSON.parse(campaignMap.properties);
      }
    } catch {
      e.properties = "Properties must be valid JSON (or leave as {}).";
    }

    setErrors(e);
    return Object.keys(e).length === 0;
  }

  const saveCampaignMapMutation = useMutation({
    mutationFn: async (request: AddCampaignMapRequest) => {
      const response = await axios.post("https://localhost:7242/map-gallery/add-campaign-map", request, {
        headers: { "Content-Type": "application/json" },
      });

      return response.data; // response.data contains parsed JSON body
    },
    onSuccess: () => {
      // make sure the error toast is hidden
      setShowErrorToast(false);

      onClose();
      onSaveSuccess("Campaign Map saved successfully."); // notify parent
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

  async function handleSave() {
    // if (!validate()) return;
    setSaving(true);
    try {
      const request: AddCampaignMapRequest = {
        id: campaignMap.id ?? undefined,
        mapName: campaignMap.mapName,
        description: campaignMap.description,
        width: campaignMap.width,
        height: campaignMap.height,
        creationEngine: 0, // hard-coded todo fix
        // optional: if your API wants parameters from the JSON field
        // safely parse; you've already validated it's JSON
        parameters: JSON.parse(campaignMap.properties || "{}"),
      };
      saveCampaignMapMutation.mutate(request);
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <Modal show={show} onHide={onClose} centered size="lg">
        <Modal.Header closeButton>
          <Modal.Title>New Campaign Map</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <Form>
            <Row>
              <Col sm={12}>
                <Form.Group as={Row} className="mb-2" controlId="Id">
                  <Form.Label column sm={3}>
                    Id
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control type="text" value={campaignMap.id ?? ""} placeholder="(auto)" disabled readOnly />
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-2" controlId="MapName">
                  <Form.Label column sm={3}>
                    Map Name
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control
                      type="text"
                      value={campaignMap.mapName}
                      onChange={(e) => set("mapName", e.currentTarget.value)}
                      isInvalid={!!errors.mapName}
                    />
                    <Form.Control.Feedback type="invalid">{errors.mapName}</Form.Control.Feedback>
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-2" controlId="Width">
                  <Form.Label column sm={3}>
                    Width
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control
                      type="number"
                      min={MIN_WIDTH}
                      max={MAX_WIDTH}
                      value={campaignMap.width}
                      onChange={(e) => set("width", parseInt(e.currentTarget.value || "0", 10))}
                      isInvalid={!!errors.width}
                    />
                    <Form.Control.Feedback type="invalid">{errors.width}</Form.Control.Feedback>
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-2" controlId="Height">
                  <Form.Label column sm={3}>
                    Height
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control
                      type="number"
                      min={MIN_HEIGHT}
                      max={MAX_HEIGHT}
                      value={campaignMap.height}
                      onChange={(e) => set("height", parseInt(e.currentTarget.value || "0", 10))}
                      isInvalid={!!errors.height}
                    />
                    <Form.Control.Feedback type="invalid">{errors.height}</Form.Control.Feedback>
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-2" controlId="CreationEngine">
                  <Form.Label column sm={3}>
                    Creation Engine
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Select value={campaignMap.creationEngine} onChange={(e) => set("creationEngine", 0)}>
                      {/* e.currentTarget.value */}
                      {engineOptions.map((o) => (
                        <option key={o} value={o}>
                          {o}
                        </option>
                      ))}
                    </Form.Select>
                  </Col>
                </Form.Group>

                <Form.Group as={Row} className="mb-2" controlId="Description">
                  <Form.Label column sm={3}>
                    Description
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control
                      as="textarea"
                      rows={3}
                      value={campaignMap.description}
                      onChange={(e) => set("description", e.currentTarget.value)}
                    />
                  </Col>
                </Form.Group>
                <Form.Group as={Row} className="mb-2" controlId="Properties">
                  <Form.Label column sm={3}>
                    Properties (JSON)
                  </Form.Label>
                  <Col sm={7}>
                    <Form.Control
                      as="textarea"
                      rows={12}
                      value={campaignMap.properties}
                      onChange={(e) => set("properties", e.currentTarget.value)}
                      isInvalid={!!errors.properties}
                    />
                    <Form.Control.Feedback type="invalid">{errors.properties}</Form.Control.Feedback>
                  </Col>
                </Form.Group>
              </Col>
            </Row>
          </Form>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>
            {saving ? "Saving..." : "Save"}
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
    </>
  );
}
