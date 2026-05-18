import React from 'react';
import { bindValue, useValue } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';
import { Icon, toHexColor } from './Icons';

interface StatEntry {
    label: string;
    value: number;
    icon: string;
    color: string;
}

interface FloatingStat {
    entries: StatEntry[];
    x: number;
    y: number;
    z: number;
}

const floatingStats$ = bindValue<FloatingStat[]>('area_of_effect', 'floatingStats', []);
const showStats$ = bindValue<boolean>('area_of_effect', 'showStats', true);
const highVis$ = bindValue<boolean>('area_of_effect', 'highVis', false);
const activeActionMap$ = bindValue<string>('input', 'activeActionMap', 'Game');
const activeScreen$ = bindValue<string>('menu', 'activeScreen', 'Game');

export const FloatingStatsOverlay: React.FC = () => {
    const stats = useValue(floatingStats$);
    const showStats = useValue(showStats$);
    const highVis = useValue(highVis$);
    const activeActionMap = useValue(activeActionMap$);
    const activeScreen = useValue(activeScreen$);

    // Hide if stats are disabled, empty, or if a major menu is open (e.g. Escape menu)
    const isGameMenuOpen = activeActionMap !== 'Game' && activeActionMap !== 'Default' || (activeScreen && activeScreen !== 'Game');

    if (!showStats || !stats || stats.length === 0 || isGameMenuOpen) return null;

    return (
        <div style={{ position: 'absolute', top: 0, left: 0, width: '100vw', height: '100vh', pointerEvents: 'none', zIndex: -1 }}>
            <div className={`${styles.overlayRoot} ${highVis ? styles.highVis : ''}`}>
                {stats.filter((stat, index, self) => 
                    index === self.findIndex(s => 
                        s.x === stat.x && 
                        s.y === stat.y && 
                        s.entries.map(e => e.label).join() === stat.entries.map(e => e.label).join()
                    )
                ).map((stat, i) => {
                    const scale = Math.max(0.5, Math.min(1.2, 150 / (stat.z + 50)));
                    return (
                        <div 
                            key={i} 
                            className={styles.statPinContainer}
                            style={{ 
                                left: `${stat.x}%`, 
                                top: `${stat.y}%`,
                                transform: `translate(-50%, -100%) scale(${scale})`
                            }}
                        >
                            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                                {stat.entries.filter((entry, idx, arr) => arr.findIndex(e => e.label === entry.label) === idx).map((entry, j) => {
                                    const parsedColor = toHexColor(entry.color);
                                    const safeColor = (parsedColor.toLowerCase() === '#ffffff' && entry.label === 'Meals') ? '#ffb84d' : parsedColor;
                                    
                                    const displayColor = highVis ? '#000000' : safeColor;
                                    const valColor = highVis ? '#ffffff' : safeColor;
                                    return (
                                        <div key={j} className={styles.statStackItem} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', marginBottom: '4rem' }}>
                                            {/* Beautiful Bubble Circle at the top */}
                                            <div className={styles.statBubbleCircle} style={{ borderColor: displayColor }}>
                                                <Icon name={entry.icon} color={displayColor} size={22} />
                                            </div>

                                            {/* Custom Slate Tag below the Circle */}
                                            <div className={styles.statLabelTag} style={{ borderColor: `${displayColor}55` }}>
                                                <span className={styles.statLabelText} style={{ color: displayColor }}>{entry.label.toUpperCase()}</span>
                                                <span className={styles.statLabelValue} style={{ color: displayColor }}>+{Math.round(entry.value)}%</span>
                                            </div>
                                        </div>
                                    );
                                })}
                                {/* Elegant Connecting Line extending down to the building */}
                                <div 
                                    className={styles.connectingLine} 
                                    style={{ 
                                        background: `linear-gradient(to top, rgba(255, 255, 255, 0.02) 0%, ${toHexColor(stat.entries[0].color)}e0 100%)` 
                                    }} 
                                />
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};
