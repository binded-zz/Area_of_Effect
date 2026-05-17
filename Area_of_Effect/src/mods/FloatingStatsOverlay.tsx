import React from 'react';
import { bindValue, useValue } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';
import { Icon } from './Icons';

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
        <div style={{ position: 'absolute', top: 0, left: 0, width: '100vw', height: '100vh', pointerEvents: 'none' }}>
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
                            <div className={styles.multiStatBubble}>
                                {stat.entries.filter((entry, idx, arr) => arr.findIndex(e => e.label === entry.label) === idx).map((entry, j) => (
                                    <div key={j} className={styles.statRow}>
                                        <div className={styles.miniIconBadge}>
                                            <Icon name={entry.icon} className={styles.miniIconSvg} color={entry.color} size={16} />
                                        </div>
                                        <div className={styles.statRowText}>
                                            <span className={styles.statName} style={{ color: entry.color }}>{entry.label}</span>
                                            <span className={styles.statVal} style={{ color: entry.color }}>+{Math.round(entry.value)}%</span>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};
