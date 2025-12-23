import { Modal } from "react-bootstrap";

export interface OpenCampaignMapDialogProps {
  show: boolean;
  //initialValues?: Partial<NewCampaignMapValues>;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

export default function OpenCampaignMapDialog({ show, onClose }: OpenCampaignMapDialogProps) {
  return (
    <Modal show={show} onHide={onClose} centered size="lg">
      <Modal.Header closeButton>
        <Modal.Title>Open Map</Modal.Title>
      </Modal.Header>
      <Modal.Body></Modal.Body>
    </Modal>
  );
}
