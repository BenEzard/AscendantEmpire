import Button from "@/components/ui/Button";

export interface UnitDesignHeaderProps {
  onClose: () => void;
}

export default function UnitDesignHeader({ onClose }: UnitDesignHeaderProps) {
  return (
    <div className="flex items-center justify-between border-b border-white/10 px-4 py-2">
      <div className="text-lg font-semibold text-white">Unit Designer</div>
      <Button variant="light" onClick={onClose}>
        Close
      </Button>
    </div>
  );
}
