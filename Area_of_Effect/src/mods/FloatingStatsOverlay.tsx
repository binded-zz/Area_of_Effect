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

const floatingStatsJson$ = bindValue<string>('area_of_effect', 'floatingStatsJson', '[]');
const showStats$ = bindValue<boolean>('area_of_effect', 'showStats', false);
const highVis$ = bindValue<boolean>('area_of_effect', 'highVis', false);
const bubbleSize$ = bindValue<number>('area_of_effect', 'bubbleSize', 100);
const activeActionMap$ = bindValue<string>('input', 'activeActionMap', 'Game');
const activeScreen$ = bindValue<string>('menu', 'activeScreen', 'Game');

const getValueText = (label: string, value: number) => {
    const lower = label.toLowerCase();
    const prefix = value >= 0 ? '+' : '';

    if (lower.includes('range')) {
        const rounded = Math.round(value || 0);
        return `${rounded} m`;
    }
    if (lower.includes('attractiveness') || lower.includes('well-being') || lower.includes('wellbeing') || (lower.includes('health') && !lower.includes('healthcare')) || lower.includes('meals')) {
        const rounded = Math.round(value || 0);
        return `${prefix}${rounded}`;
    }
    if (lower.includes('elementary') || lower.includes('high school') || lower.includes('college') || lower.includes('university')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Students`;
    }
    if (lower.includes('police patrol')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Cars`;
    }
    if (lower.includes('fire engines')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Engines`;
    }
    if (lower.includes('healthcare')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Ambulances`;
    }
    if (lower.includes('deathcare')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Hearses`;
    }
    if (lower.includes('post vans')) {
        const rounded = Math.round(value || 0);
        return `+${rounded} Vans`;
    }
    // Default: percentage. C# sends decimal modifiers (e.g. -0.8 = -80%), multiply by 100.
    const pct = Math.round((value || 0) * 100);
    const pctPrefix = pct >= 0 ? '+' : '';
    return `${pctPrefix}${pct} %`;
};

export const FloatingStatsOverlay: React.FC = () => {
    const statsJson = useValue(floatingStatsJson$);
    const showStats = useValue(showStats$);
    const highVis = useValue(highVis$);
    const bubbleSize = useValue(bubbleSize$);
    const activeActionMap = useValue(activeActionMap$);
    const activeScreen = useValue(activeScreen$);

    // Parse stats JSON safely
    const stats = React.useMemo(() => {
        if (!statsJson || statsJson === '[]') return [];
        try {
            return JSON.parse(statsJson) as FloatingStat[];
        } catch (e) {
            console.error('[AoE Stats] Failed to parse floating stats JSON:', e);
            return [];
        }
    }, [statsJson]);

    // De-duplicate stats
    const filteredStats = React.useMemo(() => {
        if (!stats) return [];
        return stats.filter((stat, index, self) =>
            stat && index === self.findIndex(s =>
                s && s.x === stat.x &&
                s.y === stat.y &&
                (s.entries ? s.entries.map(e => e?.label || '').join() : '') === (stat.entries ? stat.entries.map(e => e?.label || '').join() : '')
            )
        );
    }, [stats]);

    const isGameMenuOpen = activeScreen === 'MainMenu' || activeScreen === 'EscapeMenu';
    const isVisible = showStats && !isGameMenuOpen;

    return (
        <div
            style={{
                position: 'absolute',
                top: 0,
                left: 0,
                width: '100vw',
                height: '100vh',
                pointerEvents: 'none',
                zIndex: -1,
                display: isVisible ? 'block' : 'none'
            }}
        >
            <div className={`${styles.overlayRoot} ${highVis ? styles.highVis : ''}`}>
                {Array.from({ length: 50 }).map((_, i) => {
                    const stat = filteredStats[i];
                    const hasStat = !!stat && !!stat.entries && stat.entries.length > 0;

                    const entries = stat?.entries || [];
                    const x = stat?.x || 0;
                    const y = stat?.y || 0;
                    const z = stat?.z || 0;

                    const baseScale = Math.max(0.5, Math.min(1.2, 150 / ((z || 0) + 50)));
                    const finalScale = baseScale * (bubbleSize / 100);
                    const validEntries = entries.filter(entry => entry && entry.label);

                    const firstColor = validEntries.length > 0 && validEntries[0]?.color ? validEntries[0].color : '#ffffff';

                    return (
                        <div
                            key={i}
                            className={styles.statPinContainer}
                            style={{
                                left: `${x}%`,
                                top: `${y}%`,
                                transform: `translate(-50%, -100%) scale(${finalScale})`,
                                display: hasStat ? 'flex' : 'none'
                            }}
                        >
                            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                                {Array.from({ length: 4 }).map((_, j) => {
                                    const entry = validEntries[j];
                                    const hasEntry = !!entry && !!entry.label;

                                    const parsedColor = toHexColor(entry?.color || '#ffffff');
                                    const safeColor = (parsedColor.toLowerCase() === '#ffffff' && entry?.label === 'Meals') ? '#ffb84d' : parsedColor;

                                    const displayColor = highVis ? '#000000' : safeColor;

                                    const lower = (entry?.label || '').toLowerCase();
                                    const isSchool = lower.includes('elementary') || lower.includes('high school') || lower.includes('college') || lower.includes('university');

                                    return (
                                        <div
                                            key={j}
                                            className={styles.statStackItem}
                                            style={{
                                                display: hasEntry ? 'flex' : 'none',
                                                flexDirection: 'column',
                                                alignItems: 'center',
                                                marginBottom: '4rem'
                                            }}
                                        >
                                            {/* Beautiful Bubble Circle at the top */}
                                            <div className={styles.statBubbleCircle} style={{ borderColor: displayColor }}>
                                                {hasEntry && <Icon name={entry.icon} color={displayColor} size={22} />}
                                            </div>

                                            {/* Label tag — hidden for schools (icon-only locator) */}
                                            {!isSchool && (
                                                <div className={styles.statLabelTag} style={{ borderColor: `${displayColor}55` }}>
                                                    <div className={styles.statLabelText} style={{ color: displayColor }}>{entry?.label?.toUpperCase() || ''}</div>
                                                    <div className={styles.statLabelValue} style={{ color: displayColor }}>{getValueText(entry?.label || '', entry?.value || 0)}</div>
                                                </div>
                                            )}
                                        </div>
                                    );
                                })}
                                {/* Elegant Connecting Line extending down to the building */}
                                <div
                                    className={styles.connectingLine}
                                    style={{
                                        background: `linear-gradient(to top, rgba(255, 255, 255, 0.02) 0%, ${toHexColor(firstColor)}e0 100%)`
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
