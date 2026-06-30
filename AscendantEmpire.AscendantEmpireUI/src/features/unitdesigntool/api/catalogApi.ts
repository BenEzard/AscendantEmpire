// API service for the dev Unit Designer's full catalog read. Thin wrapper over the shared axios `http`
// client for GET /unit-design/catalog (the dev god-view that returns every body, component, tag and
// template unfiltered). Only the fields the search drawer needs are typed here.

import { http } from "@/api/http";

import type { ComponentType } from "../types/UnitComponent";

/** A component as it appears in the catalog's `components` collection (non-body components). */
interface CatalogComponent {
  id: number;
  code: string;
  displayName: string;
  componentType: ComponentType;
}

/** A body as it appears in the catalog's `bodies` collection. Bodies carry no `componentType` field. */
interface CatalogBody {
  id: number;
  code: string;
  displayName: string;
}

/** The subset of GET /unit-design/catalog we consume for the search drawer. */
interface FullCatalogResponse {
  bodies: CatalogBody[];
  components: CatalogComponent[];
}

/** A flat row in the component search list — every component type, bodies included. */
export interface ComponentListRow {
  code: string;
  displayName: string;
  componentType: ComponentType;
}

/**
 * Get every component for the search drawer as a single flat list. The catalog keeps bodies in their own
 * `bodies` collection (excluded from `components`), so we merge the two — tagging each body row with the
 * implicit "Body" component type — to match the editor, which can author bodies too.
 */
export async function getComponentList(): Promise<ComponentListRow[]> {
  const { data } = await http.get<FullCatalogResponse>("/unit-design/catalog");

  const bodyRows: ComponentListRow[] = data.bodies.map((body) => ({
    code: body.code,
    displayName: body.displayName,
    componentType: "Body",
  }));

  const componentRows: ComponentListRow[] = data.components.map((component) => ({
    code: component.code,
    displayName: component.displayName,
    componentType: component.componentType,
  }));

  return [...bodyRows, ...componentRows];
}
