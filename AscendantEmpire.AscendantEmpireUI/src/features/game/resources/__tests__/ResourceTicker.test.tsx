import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import ResourceTicker from "../tickers/ResourceTicker";
import ResourceManagerDialog from "../manager/ResourceManagerDialog";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";

function aTicker(overrides: Partial<ResourceTickerData> = {}): ResourceTickerData {
  return {
    id: 1,
    code: "wood",
    name: "Wood",
    iconPath: "/resources/wood/oak-log_40px.png",
    amount: 100,
    amountAddedPerTurn: 0,
    isLuxury: false,
    isImported: false,
    isExported: false,
    hasStrategicStockpile: false,
    demandNotMet: false,
    grade: "None",
    ...overrides,
  };
}

function Wrapper({ ticker }: { ticker: ResourceTickerData }) {
  const [show, setShow] = useState(false);
  return (
    <>
      <ResourceTicker name={ticker.name} iconPath={ticker.iconPath} amount={ticker.amount} onClick={() => setShow(true)} />
      <ResourceManagerDialog show={show} onClose={() => setShow(false)} tickers={[ticker]} selectedResourceId={null} onSelectResource={() => {}} />
    </>
  );
}

describe("ResourceTicker", () => {
  it("opens the ResourceManagerDialog when clicked", async () => {
    const user = userEvent.setup();
    render(<Wrapper ticker={aTicker()} />);

    expect(screen.queryByText("Resource Manager")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Wood: 100" }));

    expect(screen.getByText("Resource Manager")).toBeInTheDocument();
  });

  // RS-12: every amount the API sends is a placeholder zero until the quantity ticket lands, so a zero has to render rather than hide the ticker.
  it("renders a ticker holding nothing instead of hiding it", () => {
    render(<Wrapper ticker={aTicker({ amount: 0 })} />);

    expect(screen.getByRole("button", { name: "Wood: 0" })).toBeInTheDocument();
  });

  it("wears the shield of its grade, and none when ungraded", () => {
    const { rerender } = render(<ResourceTicker name="Bone Knife" iconPath="" amount={0} grade="Medium" />);

    expect(screen.getByRole("button", { name: "Bone Knife: 0" }).querySelector(".resource-ticker__quality-shield")).toBeInTheDocument();

    rerender(<ResourceTicker name="Bone Knife" iconPath="" amount={0} grade="None" />);

    expect(screen.getByRole("button", { name: "Bone Knife: 0" }).querySelector(".resource-ticker__quality-shield")).not.toBeInTheDocument();
  });
});
