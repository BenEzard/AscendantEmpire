import { useState, type ReactNode } from "react";
import Modal, { type ModalProps } from "./Modal";
import { useDialogKeyboardShortcuts } from "@/hooks/useDialogKeyboardShortcuts";

export interface PagedDialogSection<K extends string = string> {
  key: K;
  label: ReactNode;
  content: ReactNode;
  /** When true, the section's nav button is shown greyed-out and cannot be selected. */
  disabled?: boolean;
}

export interface PagedDialogProps<K extends string = string> {
  open: boolean;
  title?: ReactNode;
  onCancel: () => void;
  onCommit?: () => void;
  footer?: ReactNode;
  size?: ModalProps["size"];
  /** Explicit panel max width (number = px). Overrides `size`. See Modal's `width`. */
  width?: ModalProps["width"];
  /**
   * Fixed height for the section content region (number = px). Pins the dialog so it never changes
   * height as the user switches pages or edits content — pages taller than this scroll internally.
   * Leave undefined to size to the tallest page (the default behaviour).
   */
  height?: number | string;
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
  width,
  height,
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
    <Modal open={open} title={title} onClose={onCancel} footer={footer} size={size} width={width} panelClassName={panelClassName}>
      {header}
      {/* When `height` is set, the whole content row is pinned to that height so the dialog never resizes;
          the content column (below) scrolls when a page exceeds it. */}
      <div className="grid grid-cols-12 gap-4" style={height == null ? undefined : { height: typeof height === "number" ? `${height}px` : height }}>
        <div className="col-span-12 md:col-span-4">
          <div className="rounded-md border border-slate-200 bg-white">
            {sections.map(({ key, label, disabled }, index) => (
              <button
                key={key}
                type="button"
                disabled={disabled}
                onClick={disabled ? undefined : () => setActiveSection(key)}
                className={[
                  "block w-full px-3 py-2 text-left text-sm",
                  index > 0 ? "border-t border-slate-200" : "",
                  disabled
                    ? "cursor-not-allowed text-slate-300"
                    : activeSection === key
                      ? "nav-item-selected font-semibold"
                      : "hover:bg-slate-50",
                ]
                  .filter(Boolean)
                  .join(" ")}>
                <span className="mr-2 text-xs text-slate-300">{index + 1}</span>
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* min-h-0 lets this grid item shrink below its content so overflow-y-auto can scroll when a
            fixed `height` is in effect; with no height it's a no-op (content sizes naturally). */}
        <div className="col-span-12 md:col-span-8 min-h-0 overflow-y-auto">
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
