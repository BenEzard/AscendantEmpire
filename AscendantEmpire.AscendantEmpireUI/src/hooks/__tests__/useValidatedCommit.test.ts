// Tests for the shared "Save was pressed" policy. TagSetupDialog exercises this end-to-end through the
// DOM, but TagTypeSetupDialog uses it on a different navigation axis (selected row rather than visible
// page) and has no tests of its own, so the policy is pinned directly here.

import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { useValidatedCommit } from "../useValidatedCommit";

/** Renders the hook with sensible defaults, returning the spies alongside the hook result. */
function setup(options: { isValid?: boolean; open?: boolean } = {}) {
  const onValid = vi.fn();
  const locate = vi.fn();

  const view = renderHook(({ isValid, open }) => useValidatedCommit({ open, isValid, locate, onValid }), {
    initialProps: { isValid: options.isValid ?? true, open: options.open ?? true },
  });

  return { ...view, onValid, locate };
}

describe("revealing validation", () => {
  it("starts quiet, so a freshly opened form is not pre-littered with errors", () => {
    const { result } = setup({ isValid: false });

    expect(result.current.submitted).toBe(false);
  });

  it("reveals validation once a commit has been attempted", () => {
    const { result } = setup({ isValid: false });

    act(() => result.current.attemptCommit());

    expect(result.current.submitted).toBe(true);
  });

  it("reveals validation even when the attempt succeeds, since the flag tracks the attempt", () => {
    const { result } = setup({ isValid: true });

    act(() => result.current.attemptCommit());

    expect(result.current.submitted).toBe(true);
  });

  it("goes quiet again when the dialog is reopened", () => {
    const { result, rerender } = setup({ isValid: false });

    act(() => result.current.attemptCommit());
    expect(result.current.submitted).toBe(true);

    rerender({ isValid: false, open: false });
    rerender({ isValid: false, open: true });

    expect(result.current.submitted).toBe(false);
  });

  it("does not go quiet on an unrelated re-render while it stays open", () => {
    const { result, rerender } = setup({ isValid: false });

    act(() => result.current.attemptCommit());
    rerender({ isValid: true, open: true });

    expect(result.current.submitted).toBe(true);
  });
});

describe("gating the commit", () => {
  it("commits when there is nothing to fix", () => {
    const { result, onValid, locate } = setup({ isValid: true });

    act(() => result.current.attemptCommit());

    expect(onValid).toHaveBeenCalledTimes(1);
    expect(locate).not.toHaveBeenCalled();
  });

  it("does not commit while something is invalid", () => {
    const { result, onValid } = setup({ isValid: false });

    act(() => result.current.attemptCommit());

    expect(onValid).not.toHaveBeenCalled();
  });

  it("moves the user to the problem instead of committing", () => {
    const { result, locate } = setup({ isValid: false });

    act(() => result.current.attemptCommit());

    expect(locate).toHaveBeenCalledTimes(1);
  });

  it("commits on a later attempt once the problem is fixed", () => {
    const { result, rerender, onValid } = setup({ isValid: false });

    act(() => result.current.attemptCommit());
    expect(onValid).not.toHaveBeenCalled();

    rerender({ isValid: true, open: true });
    act(() => result.current.attemptCommit());

    expect(onValid).toHaveBeenCalledTimes(1);
  });

  it("sees the latest validity rather than the value captured when the hook first ran", () => {
    const { result, rerender, onValid } = setup({ isValid: true });

    // A form that was valid a moment ago must not commit once it stops being valid.
    rerender({ isValid: false, open: true });
    act(() => result.current.attemptCommit());

    expect(onValid).not.toHaveBeenCalled();
  });

  it("tolerates a missing locate callback, which is optional", () => {
    const onValid = vi.fn();
    const { result } = renderHook(() => useValidatedCommit({ open: true, isValid: false, onValid }));

    expect(() => act(() => result.current.attemptCommit())).not.toThrow();
    expect(onValid).not.toHaveBeenCalled();
  });
});
