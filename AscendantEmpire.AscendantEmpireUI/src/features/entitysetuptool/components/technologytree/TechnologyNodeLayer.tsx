import React from "react";
import type { TechnologyNodeState } from "../../tools/technologyTreeGraph";
import type { LaidOutTechnologyNode } from "../../tools/technologyTreeLayout";
import TechnologyNode from "./TechnologyNode";

export interface TechnologyNodeLayerProps {
  nodes: readonly LaidOutTechnologyNode[];
  /** Only the nodes that are not in the base state appear here, so an empty map means nothing is selected. */
  nodeStates: ReadonlyMap<number, TechnologyNodeState>;
  onSelect: (id: number) => void;
}

/**
 * Every technology box, in one group.
 *
 * This exists purely as a memo boundary. Panning changes the viewBox attribute, which lives on the svg element that the canvas component owns, so every pan
 * frame re-renders the canvas — and without this boundary that would cascade into a couple of hundred children sixty times a second. Its props are all
 * referentially stable during a pan, so React stops here instead.
 */
const TechnologyNodeLayer = React.memo(function TechnologyNodeLayer({ nodes, nodeStates, onSelect }: TechnologyNodeLayerProps) {
  return (
    <g data-testid="technology-node-layer">
      {nodes.map((node) => (
        <TechnologyNode key={node.id} node={node} state={nodeStates.get(node.id) ?? "base"} onSelect={onSelect} />
      ))}
    </g>
  );
});

export default TechnologyNodeLayer;
