// Shared control styling helpers for the setup/manager dialogs.
//
// These live apart from FieldRow.tsx deliberately: that module exports components, and a module
// mixing components with plain helper functions loses fast refresh for those components
// (react-refresh/only-export-components). A bare style *constant* like `inputClass` is exempt from
// that rule, which is why it can stay there; a parameterised helper is not.

import { inputClass } from "./FieldRow";

/**
 * The class for a control whose field has failed validation: `inputClass` with a red border. The `!`
 * forces the colour to win over inputClass's default slate border regardless of Tailwind's rule
 * ordering. Pass the field's error state so a call site can style a control inline:
 * `className={errorClass(!!shownErrors.code)}`.
 */
export const errorClass = (hasError: boolean) => (hasError ? `${inputClass} !border-red-500` : inputClass);
