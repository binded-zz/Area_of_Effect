/**
 * VisualizationOverlay.tsx
 *
 * Pure React SVG overlay.  All shapes are rendered as first-class JSX elements
 * so React's reconciler drives every repaint — Cohtml sees a normal DOM diff
 * and repaints correctly.  No imperative appendChild/setAttribute calls.
 *
 * We maintain a completely static DOM layout subtree (never adding or removing elements)
 * and control display with the SVG `visibility` attribute. This prevents native Cohtml/Renoir
 * crashes caused by modifying the layout tree structure during painting.
 */

import React, { useMemo } from 'react';
import { bindValue, useValue } from 'cs2/api';
import {
  VisualizationPayload,
  RoadSegment,
  PolyVertex,
  HeatNode,
} from './VisualizationTypes';

// ── Binding ─────────────────────────────────────────────────────────────────
const vizPayload$ = bindValue<VisualizationPayload>(
  'area_of_effect',
  'vizPayload',
  {
    mode:     'hidden',
    originX:  50,
    originY:  50,
    radius:   0,
    pathData: '',
    color:    '#00c8ff',
    opacity:  0.55,
  }
);

// ── Colour helpers ──────────────────────────────────────────────────────────
function hexToRgba(hex: string, alpha: number): string {
  const safeAlpha = typeof alpha === 'number' && !isNaN(alpha) && isFinite(alpha) ? alpha : 0;
  if (!hex || typeof hex !== 'string') return `rgba(0,198,255,${safeAlpha.toFixed(3)})`;
  let h = hex.replace('#', '').trim();
  if (!/^[0-9a-fA-F]{3}$|^[0-9a-fA-F]{6}$/.test(h)) {
    if (h.startsWith('rgb')) return h;
    return `rgba(0,198,255,${safeAlpha.toFixed(3)})`;
  }
  if (h.length === 3) h = h.split('').map(c => c + c).join('');
  const r = parseInt(h.substring(0, 2), 16) || 0;
  const g = parseInt(h.substring(2, 4), 16) || 0;
  const b = parseInt(h.substring(4, 6), 16) || 0;
  return `rgba(${r},${g},${b},${safeAlpha.toFixed(3)})`;
}

// ── SVG namespace (used for inline style only; JSX handles NS automatically) ──
const SVG_STYLE: React.CSSProperties = {
  position:      'absolute',
  top:           0,
  left:          0,
  width:         '100vw',
  height:        '100vh',
  pointerEvents: 'none',
  zIndex:        -1,          // same layer as FloatingStatsOverlay — always visible
  overflow:      'visible',
};

// ── Per-mode shape components ───────────────────────────────────────────────

interface RoadsShapeProps {
  segs: RoadSegment[] | null;
  vpW: number; vpH: number;
  color: string; opacity: number;
  visible: boolean;
}
const RoadsShape: React.FC<RoadsShapeProps> = ({ segs, vpW, vpH, color, opacity, visible }) => {
  const d = useMemo(() => {
    let out = '';
    if (!segs) return out;
    for (const s of segs) {
      if (!s || !s.a || !s.b || typeof s.a.x !== 'number' || typeof s.a.y !== 'number' || typeof s.b.x !== 'number' || typeof s.b.y !== 'number') continue;
      if (isNaN(s.a.x) || isNaN(s.a.y) || isNaN(s.b.x) || isNaN(s.b.y) || !isFinite(s.a.x) || !isFinite(s.a.y) || !isFinite(s.b.x) || !isFinite(s.b.y)) continue;
      out += `M${((s.a.x / 100) * vpW).toFixed(1)},${((s.a.y / 100) * vpH).toFixed(1)}`;
      out += `L${((s.b.x / 100) * vpW).toFixed(1)},${((s.b.y / 100) * vpH).toFixed(1)}`;
    }
    return out;
  }, [segs, vpW, vpH]);

  return (
    <path
      d={d || "M0,0"}
      fill="none"
      stroke={hexToRgba(color, opacity * 0.95)}
      strokeWidth={8}
      strokeLinecap="round"
      strokeLinejoin="round"
      visibility={visible && d ? "visible" : "hidden"}
    />
  );
};

interface PolygonShapeProps {
  verts: PolyVertex[] | null;
  vpW: number; vpH: number;
  color: string; opacity: number;
  visible: boolean;
}
const PolygonShape: React.FC<PolygonShapeProps> = ({ verts, vpW, vpH, color, opacity, visible }) => {
  const points = useMemo(() => {
    if (!verts || verts.length < 3) return '';
    return verts
      .filter(v => v && typeof v.x === 'number' && typeof v.z === 'number' && !isNaN(v.x) && !isNaN(v.z) && isFinite(v.x) && isFinite(v.z))
      .map(v => `${((v.x / 100) * vpW).toFixed(1)},${((v.z / 100) * vpH).toFixed(1)}`)
      .join(' ');
  }, [verts, vpW, vpH]);

  return (
    <polygon
      points={points || "0,0 0,0 0,0"}
      fill={hexToRgba(color, opacity * 0.30)}
      stroke={hexToRgba(color, opacity * 0.90)}
      strokeWidth={2.5}
      strokeLinejoin="round"
      visibility={visible && points ? "visible" : "hidden"}
    />
  );
};

interface HeatmapShapeProps {
  nodes: HeatNode[] | null;
  vpW: number; vpH: number;
  color: string;
  opacity: number;
  visible: boolean;
}
const HeatmapShape: React.FC<HeatmapShapeProps> = ({ nodes, vpW, vpH, color, opacity, visible }) => {
  const d = useMemo(() => {
    let out = '';
    if (!nodes) return out;
    const limitedNodes = nodes
      .filter(n => n && n.screenPct && typeof n.screenPct.x === 'number' && typeof n.screenPct.y === 'number' && typeof n.w === 'number' && !isNaN(n.screenPct.x) && !isNaN(n.screenPct.y) && !isNaN(n.w) && isFinite(n.screenPct.x) && isFinite(n.screenPct.y) && isFinite(n.w))
      .slice(0, 48);
    for (const n of limitedNodes) {
      const cx = (n.screenPct.x / 100) * vpW;
      const cy = (n.screenPct.y / 100) * vpH;
      const r  = Math.max(6, n.w * 30);
      out += `M${(cx - r).toFixed(1)},${cy.toFixed(1)}a${r.toFixed(1)},${r.toFixed(1)} 0 1,0 ${(r * 2).toFixed(1)},0a${r.toFixed(1)},${r.toFixed(1)} 0 1,0 ${(-r * 2).toFixed(1)},0`;
    }
    return out;
  }, [nodes, vpW, vpH]);

  return (
    <path
      d={d || "M0,0"}
      fill={hexToRgba(color, opacity * 0.6)}
      stroke={hexToRgba(color, opacity * 0.85)}
      strokeWidth={1.5}
      visibility={visible && d ? "visible" : "hidden"}
    />
  );
};

// ── Main component ──────────────────────────────────────────────────────────
export const VisualizationOverlay: React.FC = () => {
  return null;
};
