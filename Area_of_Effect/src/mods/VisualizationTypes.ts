// === VisualizationPayload - exact parity with C# VisualizationPayload struct ===
//
//  Every field name, type and casing below must match the C# IJsonWriter output
//  exactly.  Do NOT rename any field here without changing the C# side too.
//
export type VizMode =
  | "CircleOnly"
  | "RoadsOnly"
  | "ExactPolygon"
  | "CachedHeatmap"
  | "hidden";

// Parsed sub-types of pathData depending on mode
export interface RoadSegment { a: { x: number; y: number; }; b: { x: number; y: number; }; }
export interface PolyVertex  { x: number; z: number; }
export interface HeatNode    { screenPct: { x: number; y: number; }; w: number; }

// Top-level unified payload
export interface VisualizationPayload {
  mode:     VizMode;
  originX:  number;
  originY:  number;
  radius:   number;
  pathData: string; // raw JSON string - parse lazily in the renderer
  color:    string;
  opacity:  number;
}
