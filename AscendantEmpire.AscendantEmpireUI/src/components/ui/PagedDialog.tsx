import { useState, type ReactNode } from "react";
import Modal, { type ModalProps } from "./Modal";
import { useDialogKeyboardShortcuts } from "@/hooks/useDialogKeyboardShortcuts";

export interface PagedDialogSection<K extends string = string> {
  key: K;
  label: ReactNode;
  content: ReactNode;
}

export interface PagedDialogProps<K extends string = string> {
  open: boolean;
  title?: ReactNode;
  onCancel: () => void;
  onCommit?: () => void;
  footer?: ReactNode;
  size?: ModalProps["size"];
  panelClassName?: string;
  sections: readonly PagedDialogSection<K>[];
  header?: ReactNode;
  /** Provide to control the active section externally. */
  activeSection?: K;
  /** Called when the user navigates to a different section. */
  onSectionChange?: (key: K) => void;
}

export default function PagedDialog<K extends string = string>({
  open,
  title,
  onCancel,
  onCommit,
  footer,
  size,
  panelClassName,
  sections,
  header,
  activeSection: activeSectionProp,
  onSectionChange,
}: PagedDialogProps<K>) {
  const [internalSection, setInternalSection] = useState<K>(sections[0]?.key as K);

  const activeSection = activeSectionProp ?? internalSection;
  const setActiveSection = (key: K) => {
    setInternalSection(key);
    onSectionChange?.(key);
  };

  useDialogKeyboardShortcuts({
    open,
    onCommit,
    onCancel,
    sections,
    activeSection,
    onSectionChange: setActiveSection,
  });

  return (
    <Modal open={open} title={title} onClose={onCancel} footer={footer} size={size} panelClassName={panelClassName}>
      {header}
      <div className="grid grid-cols-12 gap-4">
        <div className="col-span-12 md:col-span-4">
          <div className="rounded-md border border-slate-200 bg-white">
            {sections.map(({ key, label }, index) => (
              <button
                key={key}
                type="button"
                onClick={() => setActiveSection(key)}
                className={[
                  "block w-full px-3 py-2 text-left text-sm",
                  index > 0 ? "border-t border-slate-200" : "",
                  activeSection === key ? "nav-item-selected font-semibold" : "hover:bg-slate-50",
                ]
                  .filter(Boolean)
                  .join(" ")}>
                <span className="mr-2 text-xs text-slate-300">{index + 1}</span>
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="col-span-12 md:col-span-8">
          <div className="grid">
            {sections.map(({ key, content }) => (
              <div key={key} style={{ gridArea: "1/1" }} className={activeSection !== key ? "invisible" : ""}>
                {content}
              </div>
            ))}
          </div>
        </div>
      </div>
    </Modal>
  );
}
