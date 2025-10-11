import { useState } from "react";
import { Button, Col, Form, Modal, OverlayTrigger, Row, Tooltip } from "react-bootstrap";

interface TextEditorProps {
  show: boolean;
  title: string;
  textBody: string | undefined;
  onClose: () => void;
}

function ExportDialog({ show, title = "Text", textBody, onClose }: TextEditorProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    const text = textBody ?? "";
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
      } else {
        // Fallback for older browsers
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        document.execCommand("copy");
        document.body.removeChild(ta);
      }
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    } catch {
      // Optional: handle error or show a toast
    }
  }

  return (
    <Modal show={show} onHide={onClose} centered size="lg" contentClassName="thick-border-modal">
      <Modal.Header closeButton>
        <Modal.Title>{title}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <Form>
          <Row>
            <Col sm={12}>
              <Form.Group controlId="biomeList" className="mb-3 h-100 d-flex flex-column">
                <Form.Control
                  as="textarea"
                  value={textBody}
                  readOnly
                  style={{
                    flexGrow: 1,
                    minHeight: "200px",
                    maxHeight: "400px",
                    overflowY: "auto",
                    fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace",
                    fontSize: "9pt",
                    whiteSpace: "pre", // keep JSON formatting
                  }}
                />
              </Form.Group>
            </Col>
          </Row>
        </Form>
      </Modal.Body>

      <Modal.Footer>
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
        <OverlayTrigger placement="bottom" overlay={<Tooltip id="copy-tt">Copy to clipboard</Tooltip>}>
          <Button variant={copied ? "success" : "primary"} onClick={handleCopy}>
            {copied ? "Copied!" : "Copy"}
          </Button>
        </OverlayTrigger>
      </Modal.Footer>
    </Modal>
  );
}

export default ExportDialog;
