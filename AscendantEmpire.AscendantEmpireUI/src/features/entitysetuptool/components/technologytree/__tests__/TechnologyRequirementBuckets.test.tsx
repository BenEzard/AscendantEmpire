import { render, screen } from "@testing-library/react";

import TechnologyRequirementBuckets from "../TechnologyRequirementBuckets";

// The one distinction this component exists to protect: an unused research bucket has no requirement at all, rather than a requirement of zero. A bucket
// rendered as "0" reads as "free", which is the opposite of "not part of this technology", so every case here is about telling those two apart.
//
// Purely presentational — props in, nothing out.

function renderBuckets(overrides: Partial<React.ComponentProps<typeof TechnologyRequirementBuckets>> = {}) {
  render(
    <TechnologyRequirementBuckets
      directedRequired={500}
      experienceRequired={null}
      diffusionRequired={null}
      experienceSourceCount={0}
      {...overrides}
    />,
  );
}

/** The row for one bucket, found by the test id the component stamps on it. */
function bucketRow(bucket: "directed" | "experience" | "diffusion"): HTMLElement {
  return screen.getByTestId(`technology-bucket-${bucket}`);
}

// ─── Used against unused ────────────────────────────────────────────────────────

describe("an unused bucket", () => {
  it("says so in words, and never as a zero", () => {
    renderBuckets({ directedRequired: 500, experienceRequired: null, diffusionRequired: null });

    expect(bucketRow("experience")).toHaveTextContent("Not used by this technology");
    expect(bucketRow("diffusion")).toHaveTextContent("Not used by this technology");
    expect(bucketRow("experience")).not.toHaveTextContent("0");
  });

  it("is distinguished from a bucket genuinely set to zero", () => {
    // Nothing in the seeded catalogue does this, but a zero is a real requirement the data model can express, and it must not read as "unused".
    renderBuckets({ experienceRequired: 0 });

    expect(bucketRow("experience")).toHaveTextContent("0");
    expect(bucketRow("experience")).not.toHaveTextContent("Not used by this technology");
  });
});

describe("a used bucket", () => {
  it("shows its requirement, grouped for readability", () => {
    renderBuckets({ directedRequired: 1500 });

    expect(bucketRow("directed")).toHaveTextContent("1,500");
  });

  it("shows all three when all three are in use", () => {
    renderBuckets({ directedRequired: 500, experienceRequired: 250, diffusionRequired: 250, experienceSourceCount: 1 });

    expect(bucketRow("directed")).toHaveTextContent("500");
    expect(bucketRow("experience")).toHaveTextContent("250");
    expect(bucketRow("diffusion")).toHaveTextContent("250");
    expect(screen.queryByText(/can never fill/)).not.toBeInTheDocument();
  });
});

// ─── The unfillable bucket check ────────────────────────────────────────────────

describe("an experience bucket with nothing feeding it", () => {
  it("warns, because such a technology could never be discovered", () => {
    renderBuckets({ experienceRequired: 250, experienceSourceCount: 0 });

    expect(screen.getByText(/can never fill/)).toBeInTheDocument();
  });

  it("stays quiet when the technology does not use the experience bucket at all", () => {
    renderBuckets({ experienceRequired: null, experienceSourceCount: 0 });

    expect(screen.queryByText(/can never fill/)).not.toBeInTheDocument();
  });
});
