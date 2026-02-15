import { useState } from "react";
import { Button, Modal } from "@/components/ui";

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
    <Modal
      open={show}
      onClose={onClose}
      title={title}
      size="lg"
      panelClassName="thick-border-modal"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          <span title="Copy to clipboard">
            <Button variant={copied ? "success" : "primary"} onClick={handleCopy}>
              {copied ? "Copied!" : "Copy"}
            </Button>
          </span>
        </>
      }>
      <div className="flex flex-col">
        <textarea
          value={textBody ?? ""}
          readOnly
          className="w-full resize-none rounded-md border border-slate-300 p-2 text-xs"
          style={{
            minHeight: "200px",
            maxHeight: "400px",
            overflowY: "auto",
            fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace",
            whiteSpace: "pre",
          }}
        />
      </div>
    </Modal>
  );
}

export default ExportDialog;
