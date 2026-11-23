// useFocusOnOpen — puts the caret in a dialog's first field each time the dialog opens.
//
// Modal focuses its first focusable element on open, which is the header's close button. This wins over that because it runs in
// the dialog component, and a parent's effects run after its children's. Must therefore be called by the component that renders
// the Modal, never from inside it.
//
// `isReady` covers a field that cannot take focus yet — still loading, or on a PagedDialog page that is hidden. The focus waits
// for it rather than being dropped, and is spent once taken, so the caret is not pulled back later in the same opening.

import { useEffect, useRef } from "react";

export function useFocusOnOpen<T extends HTMLElement>(open: boolean, isReady = true) {
  const targetRef = useRef<T>(null);
  const isFocusPending = useRef(false);

  useEffect(() => {
    if (open) {
      isFocusPending.current = true;
    }
  }, [open]);

  useEffect(() => {
    if (!open || !isReady || !isFocusPending.current) {
      return;
    }

    const target = targetRef.current;

    // A disabled field leaves Modal's own choice standing.
    if (target == null || target.matches(":disabled")) {
      return;
    }

    isFocusPending.current = false;
    target.focus();
  }, [open, isReady]);

  return targetRef;
}
