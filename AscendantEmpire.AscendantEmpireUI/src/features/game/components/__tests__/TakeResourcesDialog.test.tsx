import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import TakeResourcesDialog from "../TakeResourcesDialog";
import type { TakeResourcesOption, TakeResourcesResult } from "@/features/game/api/takeResourcesApi";

// The API is mocked rather than the hooks, so what is pinned is the request the dialog actually sends — the resource id
// especially, since that is the whole of what the player chooses here.
const { getTakeResourcesOptions, takeResources } = vi.hoisted(() => ({
  getTakeResourcesOptions: vi.fn(),
  takeResources: vi.fn(),
}));

vi.mock("@/features/game/api/takeResourcesApi", () => ({
  getTakeResourcesOptions,
  takeResources,
}));

function berries(overrides: Partial<TakeResourcesOption> = {}): TakeResourcesOption {
  return {
    mapResourceInstanceId: 41,
    slot: 0,
    displayName: "Berry Patch",
    iconPath: "/map-resources/berries.png",
    unitsWouldTake: 1_000,
    wouldGain: [{ assetId: 7, assetDisplayName: "Berries", amount: 1_000 }],
    ...overrides,
  };
}

function deer(overrides: Partial<TakeResourcesOption> = {}): TakeResourcesOption {
  return {
    mapResourceInstanceId: 42,
    slot: 1,
    displayName: "Deer Herd",
    iconPath: null,
    unitsWouldTake: 500,
    wouldGain: [{ assetId: 9, assetDisplayName: "Hides", amount: 250 }],
    ...overrides,
  };
}

function result(overrides: Partial<TakeResourcesResult> = {}): TakeResourcesResult {
  return {
    displayName: "Berry Patch",
    xCoord: 3,
    yCoord: 4,
    unitsTaken: 1_000,
    isExhausted: false,
    gained: [{ assetId: 7, assetDisplayName: "Berries", amount: 1_000 }],
    forces: [{ unitInstanceId: 5, displayName: "Settler", unitsTaken: 1_000 }],
    ...overrides,
  };
}

function renderDialog(props: Partial<React.ComponentProps<typeof TakeResourcesDialog>> = {}) {
  // Retries off: a test that asserts on a refusal would otherwise wait for react-query to try again first.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  const onClose = vi.fn();
  const onTaken = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <TakeResourcesDialog show gameId={1} playerId={2} forceIds={[5]} onClose={onClose} onTaken={onTaken} {...props} />
    </QueryClientProvider>,
  );

  return { onClose, onTaken };
}

beforeEach(() => {
  vi.clearAllMocks();
  getTakeResourcesOptions.mockResolvedValue({ resources: [berries()] });
  takeResources.mockResolvedValue(result());
});

describe("TakeResourcesDialog", () => {
  it("lists what the tile holds and what each one gives", async () => {
    getTakeResourcesOptions.mockResolvedValue({ resources: [berries(), deer()] });

    renderDialog();

    expect(await screen.findByText("Berry Patch")).toBeInTheDocument();
    expect(screen.getByText("Gain Berries")).toBeInTheDocument();
    expect(screen.getByText("Gain Hides")).toBeInTheDocument();
  });

  /**
   * The dialog is for choosing between resources, and the amount is the same arithmetic whichever is picked — so it says
   * nothing about the choice, and a figure shown before the order is one the dwindling patch may not honour.
   */
  it("says nothing about how much would be taken", async () => {
    getTakeResourcesOptions.mockResolvedValue({ resources: [berries(), deer()] });

    renderDialog();

    await screen.findByText("Berry Patch");

    expect(screen.queryByText(/1,000/)).not.toBeInTheDocument();
  });

  it("asks about the whole selection, since one order covers every selected force", async () => {
    renderDialog({ forceIds: [5, 9] });

    await waitFor(() => expect(getTakeResourcesOptions).toHaveBeenCalledWith([5, 9]));
  });

  /**
   * The dialog is only opened for a tile holding two, so one coming back means the tile changed since — and a lone row the
   * player still has to click would be a step that chooses nothing.
   */
  it("preselects a resource that turns out to be the only one left", async () => {
    renderDialog();

    expect(await screen.findByRole("radio", { name: "Berry Patch" })).toBeChecked();
  });

  it("sends the resource the player picked", async () => {
    getTakeResourcesOptions.mockResolvedValue({ resources: [berries(), deer()] });

    const { onTaken } = renderDialog();

    await userEvent.click(await screen.findByRole("radio", { name: "Deer Herd" }));
    await userEvent.click(screen.getByRole("button", { name: "Take" }));

    await waitFor(() => expect(takeResources).toHaveBeenCalledWith({ forceIds: [5], mapResourceInstanceId: 42 }));
    expect(onTaken).toHaveBeenCalled();
  });

  /** Two resources is the one case the dialog exists for, so neither may be chosen for the player. */
  it("confirms nothing until a resource is chosen when the tile holds two", async () => {
    getTakeResourcesOptions.mockResolvedValue({ resources: [berries(), deer()] });

    renderDialog();

    await screen.findByText("Deer Herd");

    expect(screen.getByRole("button", { name: "Take" })).toBeDisabled();
  });

  it("closes once the take has happened", async () => {
    const { onClose, onTaken } = renderDialog();

    await userEvent.click(await screen.findByRole("button", { name: "Take" }));

    await waitFor(() => expect(onTaken).toHaveBeenCalledWith(result()));
    expect(onClose).toHaveBeenCalled();
  });

  /** Every refusal here is written to be read by the player, so it is shown as it stands rather than reworded. */
  it("shows the server's refusal as it stands", async () => {
    takeResources.mockRejectedValue(new Error("There is nothing of that resource left to take here."));

    const { onClose } = renderDialog();

    await userEvent.click(await screen.findByRole("button", { name: "Take" }));

    expect(await screen.findByText("There is nothing of that resource left to take here.")).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("says so when the tile turns out to hold nothing takeable", async () => {
    getTakeResourcesOptions.mockResolvedValue({ resources: [] });

    renderDialog();

    expect(await screen.findByText("There is nothing left to take here.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Take" })).toBeDisabled();
  });

  it("asks nothing while it is closed", () => {
    renderDialog({ show: false });

    expect(getTakeResourcesOptions).not.toHaveBeenCalled();
  });
});
