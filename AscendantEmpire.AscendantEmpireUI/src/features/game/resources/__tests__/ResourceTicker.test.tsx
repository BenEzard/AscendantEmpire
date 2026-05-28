import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import ResourceTicker from "../tickers/ResourceTicker";
import ResourceManagerDialog from "../manager/ResourceManagerDialog";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";

const testTicker: ResourceTickerData = {
  id: "wood",
  name: "Wood",
  amount: 100,
};

function Wrapper() {
  const [show, setShow] = useState(false);
  return (
    <>
      <ResourceTicker name={testTicker.name} icon={<span aria-hidden="true" />} amount={testTicker.amount} onClick={() => setShow(true)} />
      <ResourceManagerDialog
        show={show}
        onClose={() => setShow(false)}
        tickers={[testTicker]}
        selectedResourceId={null}
        onSelectResource={() => {}}
      />
    </>
  );
}

describe("ResourceTicker", () => {
  it("opens the ResourceManagerDialog when clicked", async () => {
    const user = userEvent.setup();
    render(<Wrapper />);

    expect(screen.queryByText("Resource Manager")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Wood: 100" }));

    expect(screen.getByText("Resource Manager")).toBeInTheDocument();
  });
});
