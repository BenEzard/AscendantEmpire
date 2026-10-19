import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import CreateSettlementDialog from "../CreateSettlementDialog";
import type { SettleForceResult } from "@/features/game/api/settleForceApi";

// The API is mocked rather than the mutation hook, so what is pinned is the request the dialog actually sends — the trimming
// especially, which the server also does but which the dialog must not leave to it, or an untrimmed name would be shown back.
const settleForce = vi.hoisted(() => vi.fn());

vi.mock("@/features/game/api/settleForceApi", () => ({
  settleForce,
}));

function result(overrides: Partial<SettleForceResult> = {}): SettleForceResult {
  return {
    settlementId: 7,
    structureInstanceId: 91,
    name: "Riverhold",
    tier: "Village",
    population: 100,
    xCoord: 4,
    yCoord: 1,
    completionPercent: 0,
    turnsToComplete: 2,
    ...overrides,
  };
}

function renderDialog(props: Partial<React.ComponentProps<typeof CreateSettlementDialog>> = {}) {
  // Retries off: a test that asserts on a refusal would otherwise wait for react-query to try again first.
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });

  const onClose = vi.fn();
  const onFounded = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <CreateSettlementDialog
        show
        gameId={1}
        playerId={2}
        tier="Village"
        unitInstanceId={55}
        onClose={onClose}
        onFounded={onFounded}
        {...props}
      />
    </QueryClientProvider>,
  );

  return { onClose, onFounded };
}

describe("CreateSettlementDialog", () => {
  beforeEach(() => {
    settleForce.mockReset();
  });

  it("renders nothing while closed", () => {
    renderDialog({ show: false, tier: null });

    expect(screen.queryByLabelText(/name/i)).not.toBeInTheDocument();
  });

  /**
   * The button is never disabled, so a player who presses it with nothing typed is told why rather than left to work out
   * what is wrong with a button that will not press.
   */
  it("refuses an empty name with a message rather than a dead button", async () => {
    renderDialog();

    const found = screen.getByRole("button", { name: "Found village" });

    expect(found).toBeEnabled();

    await userEvent.click(found);

    expect(screen.getByText("Give the village a name.")).toBeInTheDocument();
    expect(settleForce).not.toHaveBeenCalled();
  });

  /** Whitespace is not a name either, and the dialog says so rather than sending it for the server to reject. */
  it("refuses a name that is nothing but spaces", async () => {
    renderDialog();

    await userEvent.type(screen.getByRole("textbox"), "   ");
    await userEvent.click(screen.getByRole("button", { name: "Found village" }));

    expect(screen.getByText("Give the village a name.")).toBeInTheDocument();
    expect(settleForce).not.toHaveBeenCalled();
  });

  /** What a name may be is the server's to judge, and it judges what it is sent — so the dialog sends it as typed. */
  it("sends the name as typed, leaving the trimming to the server", async () => {
    settleForce.mockResolvedValue(result());

    renderDialog();

    await userEvent.type(screen.getByRole("textbox"), "  Riverhold  ");
    await userEvent.click(screen.getByRole("button", { name: "Found village" }));

    await waitFor(() =>
      expect(settleForce).toHaveBeenCalledWith({ unitInstanceId: 55, tier: "Village", name: "  Riverhold  " }),
    );
  });

  it("caps the name at the length the server stores", () => {
    renderDialog();

    expect(screen.getByRole("textbox")).toHaveAttribute("maxlength", "30");
  });

  it("reports the founding and closes once the settlement exists", async () => {
    settleForce.mockResolvedValue(result());

    const { onClose, onFounded } = renderDialog();

    await userEvent.type(screen.getByRole("textbox"), "Riverhold");
    await userEvent.click(screen.getByRole("button", { name: "Found village" }));

    await waitFor(() => expect(onFounded).toHaveBeenCalledWith(result()));
    expect(onClose).toHaveBeenCalledOnce();
  });

  /**
   * Every refusal the server raises is already written to be read by the player — the name is taken, the ground cannot be
   * settled — so the dialog shows it as it stands rather than rewording it, and stays open so the name can be changed.
   */
  it("shows the server's refusal and stays open", async () => {
    settleForce.mockRejectedValue(new Error("There is already a settlement called 'Riverhold'."));

    const { onClose, onFounded } = renderDialog();

    await userEvent.type(screen.getByRole("textbox"), "Riverhold");
    await userEvent.click(screen.getByRole("button", { name: "Found village" }));

    expect(await screen.findByText("There is already a settlement called 'Riverhold'.")).toBeInTheDocument();
    expect(onFounded).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  /** A refusal left under a name the player has since changed would be read as a verdict on the new one. */
  it("drops the refusal as soon as the name is edited", async () => {
    settleForce.mockRejectedValue(new Error("There is already a settlement called 'Riverhold'."));

    renderDialog();

    await userEvent.type(screen.getByRole("textbox"), "Riverhold");
    await userEvent.click(screen.getByRole("button", { name: "Found village" }));

    expect(await screen.findByText(/already a settlement/)).toBeInTheDocument();

    await userEvent.type(screen.getByRole("textbox"), "2");

    expect(screen.queryByText(/already a settlement/)).not.toBeInTheDocument();
  });
});
