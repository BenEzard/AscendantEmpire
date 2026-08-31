import { renderHook } from "@testing-library/react";
import { setAuth } from "@/api/authToken";
import { useAccountMenu } from "../useAccountMenu";

describe("useAccountMenu", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("uses the signed-in username as the header label", () => {
    setAuth({ token: "t", username: "Ben" });

    const { result } = renderHook(() => useAccountMenu());

    expect(result.current.headerLabel).toBe("Ben");
  });

  it("offers a single unlabelled Log out group when signed in", () => {
    setAuth({ token: "t", username: "Ben" });

    const { result } = renderHook(() => useAccountMenu());

    // Unlabelled so HamburgerMenu draws a divider above it rather than a heading.
    expect(result.current.group?.label).toBeUndefined();
    expect(result.current.group?.items.map((item) => item.label)).toEqual(["Log out"]);
  });

  /**
   * AuthGate normally stops a logged-out user reaching a page with a toolbar, but the 401 interceptor
   * clears auth before its reload lands — this is the state the menu shows during that window.
   */
  it("prompts to log in and offers no group when there is no auth state", () => {
    const { result } = renderHook(() => useAccountMenu());

    expect(result.current.headerLabel).toBe("Please log in");
    expect(result.current.group).toBeNull();
  });
});
