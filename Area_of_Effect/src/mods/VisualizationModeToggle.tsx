/**
 * VisualizationModeToggle.tsx
 *
 * Radio-button strip for the four visualization modes plus an explicit Off button.
 *
 * Behaviour (mutually exclusive radio):
 *   - Clicking any mode button sends that index to C# and activates it.
 *   - Clicking "Off" sends -1 to C# which sets mode to hidden.
 *   - There is no toggle-off-on-same-click; Off is an explicit button.
 *
 * The C# vizMode ValueBinding drives the active highlight so the button state
 * always reflects the real backend state, not optimistic local state.
 */

import React, { useCallback } from 'react';
import { bindValue, useValue, trigger } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';

// Live binding - mirrors m_ModeBinding in VisualizationDispatcherSystem.
// Default 0 = CircleOnly. -1 = off.
const vizMode$ = bindValue<number>('area_of_effect', 'vizMode', 0);

interface ModeDescriptor {
  index: number;
  label: string;
  title: string;
}

const ALL_MODES: ReadonlyArray<ModeDescriptor> = [
  { index: -1, label: 'Off',     title: 'Off - disable visualization'              },
  { index:  0, label: 'Circle',  title: 'Circle - native 3D service radius disc'    },
  { index:  1, label: 'Roads',   title: 'Roads Only - highlight roads in range'     },
  { index:  2, label: 'Polygon', title: 'Exact Polygon - BFS isochrone boundary'    },
  { index:  3, label: 'Heatmap', title: 'Coverage Heatmap - intensity gradient'     },
];

const containerStyle: React.CSSProperties = {
  display: 'flex', flexDirection: 'column',
};
const rowStyle: React.CSSProperties = {
  display: 'flex', flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap',
};
const headingStyle: React.CSSProperties = {
  fontSize: '11rem', fontWeight: 700, color: 'rgba(255,255,255,0.55)',
  textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6rem',
};

export const VisualizationModeToggle: React.FC = () => {
  // activeMode is -1 when off, 0-3 when a mode is selected.
  const activeMode = useValue(vizMode$);
  const [hoveredIndex, setHoveredIndex] = React.useState<number | null>(null);

  const handleClick = useCallback((index: number) => {
    trigger('area_of_effect', 'setVizMode', index);
  }, []);

  return (
    <div style={containerStyle}>
      <span style={headingStyle}>Visualization Mode</span>
      <div style={rowStyle}>
        {ALL_MODES.map((m, idx) => (
          <div
            key={m.index}
            title={m.title}
            className={`${styles.footerBtn} ${activeMode === m.index ? styles.footerBtnPrimary : ''}`}
            onClick={() => handleClick(m.index)}
            onMouseEnter={() => setHoveredIndex(m.index)}
            onMouseLeave={() => setHoveredIndex(null)}
            style={{
              position: 'relative',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '6rem 12rem',
              height: 'auto',
              minWidth: '70rem',
              marginRight: idx < ALL_MODES.length - 1 ? '6rem' : '0',
            }}
          >
            <div>{m.label}</div>
            {hoveredIndex === m.index && (
              <div style={{
                position: 'absolute',
                bottom: '125%',
                backgroundColor: 'rgba(26, 38, 47, 0.95)',
                color: '#fff',
                padding: '4rem 8rem',
                borderRadius: '4rem',
                border: '1px solid rgba(255,255,255,0.15)',
                fontSize: '12rem',
                whiteSpace: 'nowrap',
                pointerEvents: 'none',
                zIndex: 1000,
                boxShadow: '0 4rem 12rem rgba(0,0,0,0.5)',
                ...(m.index === -1 
                  ? { left: '0', transform: 'none' } 
                  : m.index === 3 
                    ? { right: '0', left: 'auto', transform: 'none' } 
                    : { left: '50%', transform: 'translateX(-50%)' }
                ),
              }}>
                {m.title}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};
