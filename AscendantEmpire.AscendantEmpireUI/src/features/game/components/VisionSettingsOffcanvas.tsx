import { Form, Offcanvas } from "react-bootstrap";

interface VisionSettingsOffcanvasProps {
  show: boolean;
  onHide: () => void;
  visionRadius: number;
  setVisionRadius: (radius: number) => void;
  visionLoadWholeMap: boolean;
  setVisionLoadWholeMap: (loadWholeMap: boolean) => void;
}

function coerceNonNegativeInt(value: string): number {
  const next = Math.floor(Number(value));
  return Number.isFinite(next) ? Math.max(0, next) : 0;
}

export default function VisionSettingsOffcanvas({
  show,
  onHide,
  visionRadius,
  setVisionRadius,
  visionLoadWholeMap,
  setVisionLoadWholeMap,
}: VisionSettingsOffcanvasProps) {
  return (
    <Offcanvas show={show} onHide={onHide} placement="start">
      <Offcanvas.Header closeButton>
        <Offcanvas.Title>Check Vision Settings</Offcanvas.Title>
      </Offcanvas.Header>
      <Offcanvas.Body>
        <Form.Group className="mb-3">
          <div className="row align-items-center g-2">
            <div className="col-5">
              <Form.Label className="mb-0">Sight Range</Form.Label>
            </div>
            <div className="col-7">
              <Form.Control
                type="number"
                inputMode="numeric"
                min={0}
                step={1}
                value={visionRadius}
                onChange={(e) => setVisionRadius(coerceNonNegativeInt(e.target.value))}
              />
            </div>
          </div>
        </Form.Group>

        <Form.Group className="mb-3">
          <div className="row align-items-center g-2">
            <div className="col-5">
              <Form.Label className="mb-0">Load Whole Map</Form.Label>
            </div>
            <div className="col-7">
              <Form.Check type="checkbox" checked={visionLoadWholeMap} onChange={(e) => setVisionLoadWholeMap(e.currentTarget.checked)} />
            </div>
          </div>
        </Form.Group>
      </Offcanvas.Body>
    </Offcanvas>
  );
}
