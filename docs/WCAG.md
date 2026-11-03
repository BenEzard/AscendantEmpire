# WCAG 2.2 — the criteria that apply to Ascendant Empire

The accessibility rules the React frontend is held to, drawn from
[WCAG 2.2 Quick Reference](https://www.w3.org/WAI/WCAG22/quickref/).

**The target is Level AA.** Level A and AA criteria are in scope; **Level AAA is not** and is not
listed here. Where a criterion does not apply to this app at all, §6 says so and why — so that
"it isn't in the doc" never has to mean "nobody checked".

This is a companion to [look-and-feel.md](look-and-feel.md): that document says what the app's
visual language *is*, this one says what it has to clear. The two overlap most on colour, which is
why the selection tokens carry contrast warnings.

---

## 1. Images, icons and non-text content

Ascendant Empire is an icon-dense app — geography glyphs on map tiles, asset icons on tickers,
sprite components in the Unit Designer, status pips on forces. This section carries the most weight
of any here.

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **1.1.1** | Non-text Content | A | Every icon, sprite, glyph and image either has a text alternative that says the same thing, or is marked decorative. An icon that is the *only* carrier of meaning — a geography type, an asset, a rank insignia — needs a real name. An icon sitting beside its own label is decorative and takes `alt=""` / `aria-hidden`. |
| **1.4.5** | Images of Text | AA | Don't bake label text into a sprite or a generated map image. Render text as text and position it over the image. |
| **1.3.3** | Sensory Characteristics | A | Instructions can't rely on shape, size, position or colour alone. "Click the yellow tile" fails; "click the selected tile (outlined in yellow)" passes. |

**House rules**

- An `<img>` with no `alt` at all is always a defect. `alt=""` is a *decision*, not a default — it
  says "this adds nothing a screen reader needs", and it is wrong the moment the image is the only
  label.
- `react-icons` components take `aria-hidden` when decorative. When the icon *is* the control's
  name, the name belongs on the control (`aria-label`), not on the glyph.
- A `title` attribute is not a text alternative. It is invisible to keyboard and touch users — see
  §5 (1.4.13).

---

## 2. Colour and contrast

| # | Criterion | Level | Threshold |
|---|---|---|---|
| **1.4.1** | Use of Color | A | Colour is never the only signal. Pair it with text, a glyph, a shape or an ARIA state. |
| **1.4.3** | Contrast (Minimum) | AA | **4.5:1** for text; **3:1** for large text (≥ 24px, or ≥ 18.66px bold). |
| **1.4.11** | Non-text Contrast | AA | **3:1** for the parts of a control that identify it — its boundary, its focus ring, its checked/selected indicator — and for graphics that carry meaning. |

### What the ratios mean for this palette

Contrast is a property of a *pair*, so a token is never "accessible" on its own. These are the pairs
the app actually paints:

| Pair | Ratio | Verdict |
|---|---|---|
| White on `--bs-primary` `#0d6efd` | 4.50:1 | Passes text, exactly |
| White on `--bs-secondary` `#6c757d` | 4.69:1 | Passes text |
| White on `--bs-success` `#198754` | 4.53:1 | Passes text |
| White on `--bs-danger` `#dc3545` | 4.53:1 | Passes text |
| White on `--bs-warning` `#ffc107` | 1.63:1 | **Fails everything** |
| White on `--bs-info` `#0dcaf0` | 1.96:1 | **Fails everything** |
| `slate-500` `#64748b` on white | 4.76:1 | Passes text |
| `slate-400` `#94a3b8` on white | 2.56:1 | **Fails text and 3:1** |
| `slate-300` `#cbd5e1` border on white | 1.48:1 | **Fails 1.4.11** |
| `--color-selection` `#eab308` on white | 1.92:1 | **Fails 1.4.11** — and text, as look-and-feel.md already notes |

**Consequences to hold on to**

- **`--bs-warning` and `--bs-info` can never carry white text, and can never be a control's only
  boundary or state indicator.** They are usable as a *tint behind* dark text (the Alert and Toast
  variants mix them at 12–18% into white and then set `text-slate-800`/`900`, which clears 13:1).
- **`slate-400` is a decoration colour, not a text colour.** It fails as text on white and fails as
  a focus ring. `slate-500` is the lightest grey that passes as body text on white.
- **Selection yellow identifies state, so 1.4.11 applies to it.** A `border-selection/55` edge is
  1.44:1 against white — it cannot be the *only* indicator that something is selected. This is the
  concrete reason look-and-feel.md insists selection state is also carried in the markup
  (`aria-pressed`, `aria-current`): the ARIA state is what makes the yellow legal.
- **Translucent fills take their contrast from whatever is behind them.** `:root` in `index.css`
  sets a dark `#242424` background that several components override to white. Check a `color-mix(…,
  transparent)` against the surface it actually lands on.
- **Disabled controls are exempt** from 1.4.3 and 1.4.11, so `disabled:opacity-60` is fine.

---

## 3. Text, zoom and layout

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **1.4.4** | Resize Text | AA | Text scales to 200% with no loss of content or function. The risk is fixed-pixel boxes with `overflow: hidden` around text, not the `px` font sizes themselves — browser zoom scales those. |
| **1.4.10** | Reflow | AA | Usable at a 320 CSS px viewport width without two-dimensional scrolling. A *map grid* is exempt as content that requires two-dimensional layout; the chrome around it is not. |
| **1.4.12** | Text Spacing | AA | Nothing is clipped when the user forces line-height 1.5×, paragraph spacing 2×, letter spacing 0.12em and word spacing 0.16em. Fixed-height text containers are the usual casualty. |
| **1.3.4** | Orientation | AA | Don't lock to one orientation. Nothing here does. |

WCAG sets **no minimum font size** at A or AA. The app's 9–11px labels are therefore legal, but
they are the first thing to break under 1.4.4 and 1.4.12, so a fixed-height box around 9px text is
worth a second look.

---

## 4. Keyboard and focus

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **2.1.1** | Keyboard | A | **Every** function is reachable and operable from the keyboard. No exceptions for "it's a game surface". A map tile you can click is a control; so is a canvas node, a slider thumb, and anything reached by right-click or middle-click. |
| **2.1.2** | No Keyboard Trap | A | Focus can always be moved back out. A modal may cycle focus within itself, as long as Escape or a Close button releases it. |
| **2.1.4** | Character Key Shortcuts | A | A single-character shortcut (no modifier) must be switchable off, remappable, or active only while the relevant control has focus. Arrow keys and Escape are not covered by this. |
| **2.4.3** | Focus Order | A | Tab order follows meaning. Opening a dialog moves focus into it; closing one returns focus to whatever opened it. |
| **2.4.7** | Focus Visible | AA | A visible focus indicator in some mode of operation. `focus:outline-none` **must** be paired with a `focus-visible` replacement, and 1.4.11's 3:1 applies to that replacement. |
| **2.4.11** | Focus Not Obscured (Minimum) | AA | A focused control is not *entirely* hidden by a sticky toolbar, a drawer or an overlay. |
| **2.4.1** | Bypass Blocks | A | A way past the repeated chrome — a skip link, or proper landmarks (`<header>`, `<nav>`, `<main>`) so assistive tech can jump. |
| **3.2.1** | On Focus | A | Focus alone never changes context — no dialog opens, no navigation happens, on focus. |

**House rules**

- `role="button"` on a `<div>` is a promise with three parts: `tabIndex={0}`, a key handler for
  Enter *and* Space, and a visible focus style. Two out of three is a worse control than a plain
  `<div>`, because screen readers now announce something that cannot be used.
- A native `<button>` gets all of that for free. Reach for one first.
- For a large grid (the campaign map, the technology tree), the right pattern is **one tab stop plus
  arrow-key navigation** (roving tabindex), not 600 tab stops.

---

## 5. Pointer, targets and hover

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **2.5.1** | Pointer Gestures | A | Pinch-zoom and path-based pans need a single-pointer alternative — zoom buttons, arrow-key pan. |
| **2.5.2** | Pointer Cancellation | A | Don't act on the **down** event. Fire on `click`/`pointerup`, or make the action abortable or undoable. A `onMouseDown` that commits a change fails this. |
| **2.5.3** | Label in Name | A | A control's accessible name contains its visible label text, in the same order. Don't label a button "Save" and name it "Commit changes". |
| **2.5.7** | Dragging Movements | AA | Anything done by dragging needs a single-pointer, no-drag path to the same result — a slider thumb needs clicks on the track or a number field; a pannable canvas needs buttons. Keyboard support (§4) does not satisfy this; it asks for a *pointer* alternative. |
| **2.5.8** | Target Size (Minimum) | AA | Pointer targets are at least **24 × 24 CSS px**, or spaced so a 24px circle centred on each doesn't overlap a neighbour's. Exceptions: inline links in a sentence, targets whose size is determined by the user agent, and the equivalent function being available at full size elsewhere. |
| **1.4.13** | Content on Hover or Focus | AA | Extra content shown on hover or focus must be **dismissible** (Escape closes it without moving the pointer), **hoverable** (the pointer can move onto it without it vanishing) and **persistent** (it stays until dismissed, the pointer leaves, or it stops being valid). It must also appear on **keyboard focus**, not hover alone. |

**House rules**

- **A hover-only tooltip is an accessibility defect, not a style choice.** If a tooltip carries the
  only name or the only explanation of a control, keyboard and touch users have no route to it.
  `pointer-events: none` on the bubble breaks "hoverable" outright.
- `title` attributes are hover-only by definition. They are acceptable as *redundant* extra, never
  as the sole carrier.
- Map tiles at 24px or below are under the 2.5.8 floor. The escape hatch is the exception for
  equivalent function elsewhere — a tile inspector panel that does the same job at full size — not a
  claim that tiles are special.

---

## 6. Forms, labels and errors

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **1.3.1** | Info and Relationships | A | The label/control relationship is in the markup, not just the layout. A `<label>` sitting *next to* an input in a grid column is not associated with it. |
| **3.3.2** | Labels or Instructions | A | Every control that takes input has a label. |
| **3.3.1** | Error Identification | A | The error is described **in text**, and identifies which field it belongs to. A red border alone fails (and also fails 1.4.1). |
| **3.3.3** | Error Suggestion | AA | When the fix is known, say it. "Code must be 1–8 characters", not "Invalid". |
| **3.3.4** | Error Prevention (Legal, Financial, Data) | AA | Actions that destroy or overwrite stored data are reversible, checked, or confirmed. Deleting a map, a game or a user needs a confirmation step. |
| **1.3.5** | Identify Input Purpose | AA | `autocomplete` on the fields in WCAG's list — username, current-password, new-password, email. The login form already does this. |
| **3.3.7** | Redundant Entry | A | Don't ask for the same value twice in one process; carry it forward or offer it for selection. |
| **3.3.8** | Accessible Authentication (Minimum) | AA | Login must not depend on solving a puzzle, transcribing a CAPTCHA or recalling something, with no alternative. Username and password with paste allowed is fine. |
| **3.2.2** | On Input | A | Changing a setting doesn't navigate or re-render the world without warning. |

**House rules**

- Association is one of exactly two things: `<label htmlFor={id}>` pointing at a control with that
  `id`, or a `<label>` that **wraps** the control. Nothing else counts.
- `htmlFor` only works on *labelable* elements. Pointing it at a `<span role="switch">` associates
  nothing — that control needs `aria-label` or `aria-labelledby`.
- An inline validation message must be tied to its field with `aria-describedby`, and the field
  marked `aria-invalid`, or a screen-reader user hears neither.

---

## 7. Structure, names and status

| # | Criterion | Level | What it means here |
|---|---|---|---|
| **4.1.2** | Name, Role, Value | A | Every control exposes a name, a role, its state, and notifies changes. The most-broken criterion in any custom-component codebase. |
| **4.1.3** | Status Messages | AA | A message that appears without a context change — "Saved", "3 results", a validation summary, a failed login — is announced without moving focus. `role="status"` / `aria-live="polite"` for information, `role="alert"` for errors. |
| **1.3.2** | Meaningful Sequence | A | DOM order matches reading order. Beware absolutely-positioned overlays that read before the thing they annotate. |
| **2.4.2** | Page Titled | A | Each entry point has a descriptive `<title>`. All five already do. |
| **2.4.4** | Link Purpose (In Context) | A | Link text says where it goes. No "click here". |
| **2.4.6** | Headings and Labels | AA | Headings and labels describe what they introduce — and the heading levels form one outline per page, with a single `<h1>`. |
| **3.2.3** | Consistent Navigation | AA | Repeated chrome keeps the same relative order across the five modules. |
| **3.2.4** | Consistent Identification | AA | The same function is named the same everywhere. One icon, one name. |
| **3.2.6** | Consistent Help | A | Where a help affordance repeats across views, it sits in the same relative place. |
| **2.4.5** | Multiple Ways | AA | More than one route to each view. The landing page's module cards plus in-app navigation satisfies this. |
| **2.2.1** | Timing Adjustable | A | A time limit must be switchable off, adjustable, or extendable — **unless** it is a real-time event with other players, or longer than 20 hours. A multiplayer turn clock falls under the real-time exception; a client-side idle or dialog timeout does not. |
| **2.2.2** | Pause, Stop, Hide | A | Moving or auto-updating content lasting more than 5 seconds can be paused or hidden. Spinners that resolve quickly are exempt. |
| **2.3.1** | Three Flashes or Below Threshold | A | Nothing flashes more than three times a second. |
| **3.1.1** | Language of Page | A | `<html lang="en">`. All five entry points have it. |

**On `role="tooltip"`** — the role alone connects nothing. The bubble must be referenced by
`aria-describedby` from the trigger, or the trigger must carry the same text as its `aria-label`.
A portaled bubble that nothing points at is invisible to assistive technology.

---

## 8. Not in scope, and why

Listed so the gap is deliberate rather than accidental.

| # | Criterion | Why not |
|---|---|---|
| 1.2.1 – 1.2.5 | Audio/video alternatives, captions, audio description | The frontend ships no `<audio>`, no `<video>` and no sound. **Revisit the moment it does** — audio cues for turn events would pull 1.2.1 and 1.4.2 straight back in. |
| 1.4.2 | Audio Control | As above. |
| 2.5.4 | Motion Actuation | Nothing is driven by device motion. |
| 3.1.2 | Language of Parts | Single-language UI. |
| 4.1.1 | Parsing | Removed in WCAG 2.2 — it now always passes, and the quickref keeps it only for continuity with 2.0/2.1. Well-formed JSX output makes it moot regardless. |
| All AAA | — | Above the AA target. |

---

## 9. Review checklist

What to run over a changed view before calling it done.

**Keyboard only, mouse unplugged**

1. Tab through the view. Can every action be reached?
2. Is focus visible at every stop, at 3:1 or better against its surroundings?
3. Open a dialog: does focus go in, stay in, and come back out to the trigger?
4. Does Escape close dialogs, drawers and tooltips?
5. Is any information available only on hover?

**Colour and contrast**

6. Measure text against its real background — 4.5:1, or 3:1 for large text.
7. Measure control borders, focus rings and selected-state indicators — 3:1.
8. Switch the display to greyscale. Is anything now unreadable? That's a 1.4.1 failure.

**Structure**

9. One `<h1>`, headings in order, `<main>` present.
10. Every input traced to a label by `htmlFor` or wrapping — not by proximity.
11. Every icon-only control has a name that matches what it looks like it does.
12. Validation errors are in text, tied to their field, and announced.

**Scale**

13. Zoom to 200%. Anything clipped or unreachable?
14. Narrow to 320px. Does the chrome reflow? (The map grid itself is exempt.)
15. Are pointer targets 24 × 24 px, or adequately spaced?
