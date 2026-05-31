import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useEffect, useId } from "react";
import ResourceSliderCollection, { useResourceRemainder } from "../ResourceSliderCollection";
import { useSliderCollection } from "../../../../../components/ui/slider/SliderCollectionContext";
import type { LockedState } from "../../../../../components/ui/slider/Slider";

// ── Test helpers ──────────────────────────────────────────────────────────────

interface SliderConsumerProps {
  numericId: number;
  code: string;
  initialValue?: number;
  lockedState?: LockedState;
  /** Value requested when the "set" button is clicked. */
  targetValue?: number;
}

/**
 * Registers with the SliderCollectionContext and exposes reactive value/lockState
 * text plus action buttons — avoids real drag interaction in tests.
 */
function SliderConsumer({ numericId, code, initialValue = 50, lockedState = "unlocked", targetValue = 80 }: SliderConsumerProps) {
  const id = useId();
  const collection = useSliderCollection()!;

  useEffect(() => {
    collection.register(id, numericId, code, initialValue, lockedState);
    return () => collection.unregister(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const entry = collection.getEntry(id);

  return (
    <div>
      <span data-testid={`value-${code}`}>{entry?.value ?? "-"}</span>
      <span data-testid={`lockState-${code}`}>{entry?.lockState ?? "-"}</span>
      <button onClick={() => collection.requestValueUpdate(id, targetValue)} data-testid={`set-${code}`}>
        set
      </button>
      <button onClick={() => collection.requestLockToggle(id)} data-testid={`toggle-lock-${code}`}>
        lock
      </button>
    </div>
  );
}

/**
 * Registers with the ResourceRemainderContext.
 * Uses String(numericId) as the candidate ID, matching ResourceSlider behaviour,
 * so onRemainderChange receives the expected numeric value.
 */
function RemainderConsumer({ numericId, initialWill = false }: { numericId: number; initialWill?: boolean }) {
  const sliderId = String(numericId);
  const remainder = useResourceRemainder()!;

  useEffect(() => {
    remainder.registerCandidate(sliderId, initialWill);
    return () => remainder.unregisterCandidate(sliderId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div>
      <span data-testid={`remainder-${numericId}`}>{remainder.activeRemainderId === sliderId ? "active" : "inactive"}</span>
      <button onClick={() => remainder.requestRemainderToggle(sliderId)} data-testid={`toggle-remainder-${numericId}`}>
        toggle
      </button>
    </div>
  );
}

// ── onChange ──────────────────────────────────────────────────────────────────

describe("onChange", () => {
  it("fires with slider snapshots after mount", () => {
    const onChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" initialValue={60} />
      </ResourceSliderCollection>,
    );

    const lastCall: unknown[] = onChange.mock.calls.at(-1)![0];
    expect(lastCall).toHaveLength(1);
    expect(lastCall[0]).toMatchObject({ id: 1, code: "food", value: 60, lockState: "unlocked" });
  });

  it("fires again with updated value after requestValueUpdate", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" initialValue={50} targetValue={70} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("set-food"));

    const lastCall: unknown[] = onChange.mock.calls.at(-1)![0];
    expect(lastCall[0]).toMatchObject({ id: 1, code: "food", value: 70 });
  });

  it("fires with updated lockState after requestLockToggle", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-lock-food"));

    const lastCall: unknown[] = onChange.mock.calls.at(-1)![0];
    expect(lastCall[0]).toMatchObject({ id: 1, lockState: "locked" });
  });
});

// ── Value rebalancing ─────────────────────────────────────────────────────────

describe("value rebalancing", () => {
  it("distributes excess to the other unlocked slider", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    // food=50, wood=50 → total=100. Request food→80, excess=30 → wood→20.
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" initialValue={50} targetValue={80} />
        <SliderConsumer numericId={2} code="wood" initialValue={50} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("set-food"));

    const snapshots: { code: string; value: number }[] = onChange.mock.calls.at(-1)![0];
    expect(snapshots.find((s) => s.code === "food")?.value).toBe(80);
    expect(snapshots.find((s) => s.code === "wood")?.value).toBe(20);
  });

  it("leaves other sliders unchanged when there is no excess", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    // food=50, wood=50. Request food→30, total=80 < 100, no excess.
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" initialValue={50} targetValue={30} />
        <SliderConsumer numericId={2} code="wood" initialValue={50} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("set-food"));

    const snapshots: { code: string; value: number }[] = onChange.mock.calls.at(-1)![0];
    expect(snapshots.find((s) => s.code === "food")?.value).toBe(30);
    expect(snapshots.find((s) => s.code === "wood")?.value).toBe(50);
  });

  it("caps the requested value at 100 − lockedTotal", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    // food=50 (locked), wood=50. Request wood→90, cap = 100-50 = 50 → wood stays 50.
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="food" initialValue={50} lockedState="locked" />
        <SliderConsumer numericId={2} code="wood" initialValue={50} targetValue={90} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("set-wood"));

    const snapshots: { code: string; value: number }[] = onChange.mock.calls.at(-1)![0];
    expect(snapshots.find((s) => s.code === "wood")?.value).toBe(50);
    // Locked slider unchanged
    expect(snapshots.find((s) => s.code === "food")?.value).toBe(50);
  });

  it("distributes excess equally across three unlocked sliders", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    // a=34, b=33, c=33. Request a→70, excess = 70+33+33-100 = 36.
    // Distribute 36 across b(33) and c(33): base=18, rem=0 → b=15, c=15.
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onChange={onChange}>
        <SliderConsumer numericId={1} code="a" initialValue={34} targetValue={70} />
        <SliderConsumer numericId={2} code="b" initialValue={33} />
        <SliderConsumer numericId={3} code="c" initialValue={33} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("set-a"));

    const snapshots: { code: string; value: number }[] = onChange.mock.calls.at(-1)![0];
    const a = snapshots.find((s) => s.code === "a")!.value;
    const b = snapshots.find((s) => s.code === "b")!.value;
    const c = snapshots.find((s) => s.code === "c")!.value;
    expect(a + b + c).toBe(100);
    expect(a).toBe(70);
  });
});

// ── Lock toggle ───────────────────────────────────────────────────────────────

describe("lock toggle", () => {
  it("toggles an unlocked slider to locked", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <SliderConsumer numericId={1} code="food" lockedState="unlocked" />
      </ResourceSliderCollection>,
    );

    expect(screen.getByTestId("lockState-food")).toHaveTextContent("unlocked");
    await user.click(screen.getByTestId("toggle-lock-food"));
    expect(screen.getByTestId("lockState-food")).toHaveTextContent("locked");
  });

  it("toggles a locked slider back to unlocked", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <SliderConsumer numericId={1} code="food" lockedState="locked" />
      </ResourceSliderCollection>,
    );

    expect(screen.getByTestId("lockState-food")).toHaveTextContent("locked");
    await user.click(screen.getByTestId("toggle-lock-food"));
    expect(screen.getByTestId("lockState-food")).toHaveTextContent("unlocked");
  });

  it("does not toggle a systemlocked slider", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <SliderConsumer numericId={1} code="food" lockedState="systemlocked" />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-lock-food"));
    expect(screen.getByTestId("lockState-food")).toHaveTextContent("systemlocked");
  });

  it("does not toggle an unlockable slider", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <SliderConsumer numericId={1} code="food" lockedState="unlockable" />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-lock-food"));
    expect(screen.getByTestId("lockState-food")).toHaveTextContent("unlockable");
  });

  it("does not toggle a systemlockedinvisible slider", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <SliderConsumer numericId={1} code="food" lockedState="systemlockedinvisible" />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-lock-food"));
    expect(screen.getByTestId("lockState-food")).toHaveTextContent("systemlockedinvisible");
  });
});

// ── Remainder tracking ────────────────────────────────────────────────────────

describe("remainder tracking", () => {
  it("first candidate automatically becomes the active remainder", () => {
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <RemainderConsumer numericId={1} />
      </ResourceSliderCollection>,
    );
    expect(screen.getByTestId("remainder-1")).toHaveTextContent("active");
  });

  it("second candidate does not displace the first as active", () => {
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <RemainderConsumer numericId={1} />
        <RemainderConsumer numericId={2} />
      </ResourceSliderCollection>,
    );
    expect(screen.getByTestId("remainder-1")).toHaveTextContent("active");
    expect(screen.getByTestId("remainder-2")).toHaveTextContent("inactive");
  });

  it("requestRemainderToggle switches the active candidate to another", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <RemainderConsumer numericId={1} />
        <RemainderConsumer numericId={2} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-remainder-2"));

    expect(screen.getByTestId("remainder-1")).toHaveTextContent("inactive");
    expect(screen.getByTestId("remainder-2")).toHaveTextContent("active");
  });

  it("sole candidate cannot be deselected via toggle", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <RemainderConsumer numericId={1} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-remainder-1"));
    expect(screen.getByTestId("remainder-1")).toHaveTextContent("active");
  });

  it("active candidate with multiple candidates can be deselected (sets none)", async () => {
    const user = userEvent.setup();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000}>
        <RemainderConsumer numericId={1} />
        <RemainderConsumer numericId={2} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-remainder-1"));
    expect(screen.getByTestId("remainder-1")).toHaveTextContent("inactive");
    expect(screen.getByTestId("remainder-2")).toHaveTextContent("inactive");
  });

  it("onRemainderChange fires with the numeric id when first candidate registers", () => {
    const onRemainderChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onRemainderChange={onRemainderChange}>
        <RemainderConsumer numericId={5} />
      </ResourceSliderCollection>,
    );
    expect(onRemainderChange).toHaveBeenCalledWith(5);
  });

  it("onRemainderChange fires with the new numeric id after toggle", async () => {
    const user = userEvent.setup();
    const onRemainderChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onRemainderChange={onRemainderChange}>
        <RemainderConsumer numericId={1} />
        <RemainderConsumer numericId={2} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-remainder-2"));
    expect(onRemainderChange).toHaveBeenLastCalledWith(2);
  });

  it("onRemainderChange fires with null when the active candidate is deselected", async () => {
    const user = userEvent.setup();
    const onRemainderChange = vi.fn();
    render(
      <ResourceSliderCollection resourcesAddedPerTurn={1000} onRemainderChange={onRemainderChange}>
        <RemainderConsumer numericId={1} />
        <RemainderConsumer numericId={2} />
      </ResourceSliderCollection>,
    );

    await user.click(screen.getByTestId("toggle-remainder-1")); // deselect active
    expect(onRemainderChange).toHaveBeenLastCalledWith(null);
  });
});
