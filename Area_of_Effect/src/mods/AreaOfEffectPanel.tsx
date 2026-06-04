/**
 * AreaOfEffectPanel.tsx
 *
 * Main custom panel.
 *
 * Layers tab  : Selected Building stats, VisualizationModeToggle, Local Effects, Global Layers.
 * Settings tab: Visual Config sliders, Display Options, and Visualization Mode on/off toggles.
 *
 * vizModeEnabled is pure React state lifted here � no C# ModSetting required.
 * It controls which buttons appear in VisualizationModeToggle.
 */

import React, { useState, useRef, useCallback } from 'react';
import { bindValue, useValue, trigger } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';
import { Icon } from './Icons';
import { DraggableWindow, DragContext } from './DraggableWindow';
import { VanillaComponentResolver } from './VanillaComponents';
import { VisualizationModeToggle } from './VisualizationModeToggle';

// ─── PanelHeader (drag handle) ───────────────────────────────────────────────
const PanelHeader: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const onDrag = React.useContext(DragContext);
    return <div className={styles.header} onMouseDown={onDrag ?? undefined}>{children}</div>;
};
// ─── ScrollBar overlay ─────────────────────────────────────────────────────────────
// Cohtml doesn't render CSS scrollbar pseudo-elements.
// This wraps the scroll container and overlays a visible track + draggable thumb.
const ScrollBar: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const viewRef  = useRef<HTMLDivElement>(null);
    const dragRef  = useRef<{ startY: number; startScroll: number } | null>(null);
    const [bar, setBar] = React.useState({ show: false, top: 0, height: 0 });
    const [hovered, setHovered] = React.useState(false);

    const recalc = React.useCallback(() => {
        const el = viewRef.current;
        if (!el) return;
        const { scrollTop, scrollHeight, clientHeight } = el;
        
        if (scrollHeight <= clientHeight + 1) {
            setBar(prev => prev.show ? { ...prev, show: false } : prev);
            return;
        }

        const trackHeight = Math.max(clientHeight - 16, 0);
        if (trackHeight <= 0) {
            setBar(prev => prev.show ? { ...prev, show: false } : prev);
            return;
        }

        const thumbHeight = Math.max((clientHeight / scrollHeight) * trackHeight, 50);
        const maxTop = trackHeight - thumbHeight;
        const thumbTop = (scrollTop / (scrollHeight - clientHeight)) * maxTop;

        setBar(prev => {
            if (prev.show === true && prev.height === thumbHeight && prev.top === thumbTop) {
                return prev;
            }
            return { show: true, height: thumbHeight, top: thumbTop };
        });
    }, []);

    React.useEffect(() => {
        const timer = setTimeout(recalc, 0);
        return () => clearTimeout(timer);
    });

    React.useEffect(() => {
        const handleMouseMove = (e: MouseEvent) => {
            if (!dragRef.current || !viewRef.current) return;
            const el = viewRef.current;
            const dy = e.clientY - dragRef.current.startY;
            
            const trackHeight = Math.max(el.clientHeight - 16, 0);
            const thumbHeight = bar.height;
            const maxTop = trackHeight - thumbHeight;
            if (maxTop <= 0) return;
            
            const scrollRange = el.scrollHeight - el.clientHeight;
            el.scrollTop = Math.max(0, Math.min(
                dragRef.current.startScroll + (dy / maxTop) * scrollRange,
                scrollRange
            ));
        };

        const handleMouseUp = () => {
            dragRef.current = null;
        };

        window.addEventListener('mousemove', handleMouseMove);
        window.addEventListener('mouseup', handleMouseUp);
        return () => {
            window.removeEventListener('mousemove', handleMouseMove);
            window.removeEventListener('mouseup', handleMouseUp);
        };
    }, [bar.height]);

    const onThumbDown = (e: React.MouseEvent) => {
        e.preventDefault(); e.stopPropagation();
        dragRef.current = { startY: e.clientY, startScroll: viewRef.current?.scrollTop ?? 0 };
    };

    return (
        <div
            style={{ position: 'relative', flex: '1 1 auto', minHeight: 0, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}
        >
            <div ref={viewRef} onScroll={recalc} className={styles.scrollBody}>
                {children}
            </div>
            {bar.show && (
                <div 
                    onMouseEnter={() => setHovered(true)}
                    onMouseLeave={() => setHovered(false)}
                    style={{
                        position: 'absolute', right: '8px', top: '8px', bottom: '8px',
                        width: '8px', background: 'rgba(255,255,255,0.07)', borderRadius: '4px',
                        pointerEvents: 'auto',
                    }}
                >
                    <div
                        onMouseDown={onThumbDown}
                        style={{
                            position: 'absolute', left: 0, right: 0,
                            top: `${bar.top}px`, height: `${bar.height}px`,
                            background: hovered ? 'rgba(255,255,255,0.5)' : 'rgba(255,255,255,0.25)', 
                            borderRadius: '4px',
                            cursor: 'ns-resize', pointerEvents: 'auto',
                            transition: 'background 0.15s ease',
                        }}
                    />
                </div>
            )}
        </div>
    );
};

// ??? Data shapes ?????????????????????????????????????????????????????????????
interface LayerSetting {
    id: string; name: string; enabled: boolean; opacity: number;
    r: number; g: number; b: number; a: number;
}

// ??? AoeToggle (CSS-only, works in Cohtml) ????????????????????????????????????
const AoeToggle: React.FC<{ checked: boolean; onChange: () => void }> = ({ checked, onChange }) => (
    <div
        className={`${styles.toggleSwitch} ${checked ? styles.toggleSwitchOn : ''}`}
        onClick={(e) => { e.stopPropagation(); onChange(); }}
    >
        <div className={`${styles.toggleThumb} ${checked ? styles.toggleThumbOn : ''}`} />
    </div>
);

// ??? Helpers ??????????????????????????????????????????????????????????????????
function layerToHex(layer: LayerSetting): string {
    if (typeof layer.r !== 'number' || isNaN(layer.r) ||
        typeof layer.g !== 'number' || isNaN(layer.g) ||
        typeof layer.b !== 'number' || isNaN(layer.b)) return '#ffffff';
    const h = (v: number) => Math.round(v * 255).toString(16).padStart(2, '0');
    return `#${h(layer.r)}${h(layer.g)}${h(layer.b)}`;
}

// ??? Bindings ?????????????????????????????????????????????????????????????????
const globalSettings$       = bindValue<string> ('area_of_effect', 'globalSettings',      '[]');
const localSettings$        = bindValue<string> ('area_of_effect', 'localSettings',       '[]');
const buildingName$         = bindValue<string> ('area_of_effect', 'buildingName',         '');
const buildingServiceReach$ = bindValue<number> ('area_of_effect', 'buildingServiceReach', 0);
const coverageRange$        = bindValue<number> ('area_of_effect', 'coverageRange',         0);
const coverageMagnitude$    = bindValue<number> ('area_of_effect', 'coverageMagnitude',     0);
const modifierRange$        = bindValue<number> ('area_of_effect', 'modifierRange',         0);
const modifierMagnitude$    = bindValue<number> ('area_of_effect', 'modifierMagnitude',     0);
const showStats$            = bindValue<boolean>('area_of_effect', 'showStats',            true);
const showOnHover$          = bindValue<boolean>('area_of_effect', 'showOnHover',          true);
const enablePreplacement$   = bindValue<boolean>('area_of_effect', 'enablePreplacement',   true);
export const enableMiniInspector$ = bindValue<boolean>('area_of_effect', 'enableMiniInspector', true);
const highVis$              = bindValue<boolean>('area_of_effect', 'highVis',              false);
const maxDistance$          = bindValue<number> ('area_of_effect', 'maxDistance',          1000);
const bubbleSize$           = bindValue<number> ('area_of_effect', 'bubbleSize',           100);
const globalOpacity$        = bindValue<number> ('area_of_effect', 'opacity',              100);
const globalSize$           = bindValue<number> ('area_of_effect', 'circleSize',           500);
const globalHeight$         = bindValue<number> ('area_of_effect', 'overlayHeight',        0);
const preset$               = bindValue<number> ('area_of_effect', 'preset',               0);
const presets$              = bindValue<string> ('area_of_effect', 'presetsJson',          '[]');
const windowX$              = bindValue<number> ('area_of_effect', 'windowX',              100);
const windowY$              = bindValue<number> ('area_of_effect', 'windowY',              100);
const buildingEffects$      = bindValue<string> ('area_of_effect', 'buildingEffectsJson',  '[]');

interface BuildingEffect {
    group: string;
    name: string;
    value: string;
    range: string;
    icon: string;
    color: string;
}

const BuildingEffectRow: React.FC<{ effect: BuildingEffect }> = ({ effect }) => {
    return (
        <div className={styles.effectRow}>
            <div className={styles.effectLeft}>
                <div className={styles.effectIconWrap}>
                    <Icon name={effect.icon || 'Healthcare'} color={effect.color || '#ffffff'} size={18} />
                </div>
                <div className={styles.effectMeta}>
                    <div className={styles.effectName}>
                        {effect.name}
                    </div>
                    <div className={styles.effectGroup}>
                        {effect.group}
                    </div>
                </div>
            </div>
            <div className={styles.effectRight}>
                {effect.value && (
                    <div 
                        className={effect.color ? undefined : styles.effectValue} 
                        style={effect.color ? { color: effect.color } : undefined}
                    >
                        {effect.value}
                    </div>
                )}
                {effect.range && (
                    <div className={styles.effectRange}>
                        {effect.range}
                    </div>
                )}
            </div>
        </div>
    );
};

// ─── LayerRow ─────────────────────────────────────────────────────────────────
const LayerRow = React.memo<{
    layer: LayerSetting;
    onToggle: (id: string, enabled: boolean) => void;
    onAlpha:  (id: string, val: number) => void;
    onColor:  (id: string, hex: string) => void;
}>(({ layer, onToggle, onAlpha, onColor }) => {
    const Slider     = VanillaComponentResolver.instance?.Slider     || (() => null);
    const ColorField = VanillaComponentResolver.instance?.ColorField || (() => null);
    const color = layerToHex(layer);

    const [displayColor,   setDisplayColor]   = React.useState({ r: layer.r, g: layer.g, b: layer.b, a: layer.a });
    const [displayOpacity, setDisplayOpacity] = React.useState((layer.opacity || 0) * 100);

    React.useEffect(() => {
        setDisplayColor({ r: layer.r, g: layer.g, b: layer.b, a: layer.a });
        setDisplayOpacity((layer.opacity || 0) * 100);
    }, [layer]);

    const handleColorChange = React.useCallback((c: { r: number; g: number; b: number; a: number }) => {
        setDisplayColor(c);
        const h = (v: number) => Math.round(v * 255).toString(16).padStart(2, '0');
        onColor(layer.id, `#${h(c.r)}${h(c.g)}${h(c.b)}`);
    }, [layer.id, onColor]);

    const handleAlphaChange = React.useCallback((val: number) => {
        setDisplayOpacity(val);
        onAlpha(layer.id, val);
    }, [layer.id, onAlpha]);

    return (
        <div style={{ marginBottom: '8rem', paddingBottom: '6rem', borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
            <div className={styles.layerRowNative} style={{ justifyContent: 'space-between', position: 'relative' }}>
                <div style={{ display: 'flex', alignItems: 'center', flex: 1, minWidth: 0, maxWidth: '270rem', position: 'relative', zIndex: 1 }}>
                    <AoeToggle checked={layer.enabled} onChange={() => onToggle(layer.id, layer.enabled)} />
                    <div className={styles.layerIconWrapNative} style={{ margin: '0 4rem' }}>
                        <Icon name={layer.id} color={color} size={22} />
                    </div>
                    <div style={{ position: 'relative', display: 'flex', alignItems: 'center', flex: 1, minWidth: 0, overflow: 'hidden', cursor: 'pointer' }}>
                        <div className={styles.bracketLabelNative} style={{ color, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', flex: 1 }}>
                            {layer.name}
                        </div>
                        <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, opacity: 0, overflow: 'hidden', cursor: 'pointer' }}>
                            <div style={{ transform: 'scale(10, 2)', transformOrigin: 'top left', width: '100%', height: '100%' }}>
                                <ColorField value={displayColor} onChange={handleColorChange} />
                            </div>
                        </div>
                    </div>
                </div>
                <div className={styles.sliderContainerNative} style={{ position: 'relative', zIndex: 10 }}>
                    <div className={styles.sliderWrap}>
                        <Slider value={displayOpacity} start={0} end={100} onChange={handleAlphaChange} />
                    </div>
                    <div className={styles.sliderValText}>{`${Math.round(displayOpacity)} %`}</div>
                </div>
            </div>
        </div>
    );
});

const ChevronIcon: React.FC<{ collapsed: boolean }> = ({ collapsed }) => (
    <svg 
        width="18" 
        height="18" 
        viewBox="0 0 24 24" 
        fill="none" 
        stroke="currentColor" 
        strokeWidth="3" 
        strokeLinecap="round" 
        strokeLinejoin="round"
        style={{ 
            transform: collapsed ? 'rotate(-90deg)' : 'rotate(0deg)', 
            transition: 'transform 0.15s ease',
            marginRight: '8rem',
            color: '#00A2E8',
            flexShrink: 0
        }}
    >
        <polyline points="6 9 12 15 18 9" />
    </svg>
);

// ─── SectionHeader ────────────────────────────────────────────────────────────
const SectionHeader: React.FC<{
    title: string; collapsed: boolean; onCollapse: () => void;
    allEnabled?: boolean; onToggleAll?: () => void;
}> = ({ title, collapsed, onCollapse, allEnabled, onToggleAll }) => (
    <div 
        className={styles.cardHeader} 
        onClick={onCollapse} 
        style={{ cursor: 'pointer', userSelect: 'none', display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}
    >
        <div style={{ display: 'flex', alignItems: 'center', flexGrow: 1, minWidth: 0 }}>
            <ChevronIcon collapsed={collapsed} />
            <div className={styles.sectionTitle} style={{ margin: 0 }}>{title}</div>
        </div>
        {onToggleAll !== undefined && allEnabled !== undefined && (
            <AoeToggle checked={allEnabled} onChange={onToggleAll} />
        )}
    </div>
);

// ??? SettingsSliderRow ????????????????????????????????????????????????????????
const SettingsSliderRow = React.memo<{
    label: string; value: number; min: number; max: number; unit: string;
    onChange: (v: number) => void;
}>(({ label, value, min, max, unit, onChange }) => {
    const Slider = VanillaComponentResolver.instance?.Slider || (() => null);
    const [displayVal, setDisplayVal] = React.useState(value);
    React.useEffect(() => { setDisplayVal(value); }, [value]);
    const handleChange = React.useCallback((val: number) => { setDisplayVal(val); onChange(val); }, [onChange]);
    return (
        <div className={styles.layerRowNative} style={{ marginBottom: '6rem' }}>
            <div className={styles.statRowLabel} style={{ flex: 1, minWidth: 0, maxWidth: '270rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', marginRight: '10rem' }}>{label}</div>
            <div className={styles.sliderContainerSettings}>
                <div className={styles.sliderWrapSettings}>
                    <Slider value={displayVal} start={min} end={max} onChange={handleChange} />
                </div>
                <div className={styles.sliderValText}>{`${Math.round(displayVal)}${unit}`}</div>
            </div>
        </div>
    );
});

// ??? SettingsToggleRow ????????????????????????????????????????????????????????
const SettingsToggleRow: React.FC<{
    label: string; checked: boolean; onChange: () => void;
}> = ({ label, checked, onChange }) => (
    <div className={styles.layerRowNative} style={{ marginBottom: '8rem' }}>
        <div className={styles.statRowLabel} style={{ flexGrow: 1 }}>{label}</div>
        <AoeToggle checked={checked} onChange={onChange} />
    </div>
);

// ??? Main Panel ???????????????????????????????????????????????????????????????
export const AreaOfEffectPanel: React.FC = () => {
    const buildingName         = useValue(buildingName$);
    const hasSelectedBuilding  = buildingName && buildingName !== 'No Building Selected' && buildingName !== '';

    const [activeTab,       setActiveTab]       = useState<'selected' | 'layers' | 'settings' | 'presets'>(
        hasSelectedBuilding ? 'selected' : 'layers'
    );
    const [localCollapsed,  setLocalCollapsed]  = useState(false);
    const [globalCollapsed, setGlobalCollapsed] = useState(false);
    const [showResetModal,  setShowResetModal]  = useState(false);
    const [resetType,       setResetType]       = useState<'colors' | 'config'>('colors');
    const [showSavePresetModal, setShowSavePresetModal] = useState(false);
    const [selectedPresetSlot,  setSelectedPresetSlot]  = useState<number | null>(null);
    const [presetNameInput,     setPresetNameInput]     = useState('');
    const [deletePresetSlot,    setDeletePresetSlot]    = useState<number | null>(null);

        // Drag-freeze: freeze C# binding re-renders while a slider is being dragged.
    const isDraggingRef  = useRef(false);
    const dragTimeoutRef = useRef<any>(null);
    const [forceRender, setForceRender] = useState(0);
    const markDragging = useCallback(() => {
        isDraggingRef.current = true;
        if (dragTimeoutRef.current) clearTimeout(dragTimeoutRef.current);
        dragTimeoutRef.current = setTimeout(() => {
            isDraggingRef.current = false;
            setForceRender(v => v + 1);
        }, 500);
    }, []);

    // ?? C# bindings ???????????????????????????????????????????????????????
    const globalSettingsRaw = useValue(globalSettings$);
    const localSettingsRaw  = useValue(localSettings$);

    const prevGlobalRef = useRef<LayerSetting[]>([]);
    const globalSettings: LayerSetting[] = React.useMemo(() => {
        if (isDraggingRef.current) return prevGlobalRef.current;
        try { const p = JSON.parse(globalSettingsRaw); prevGlobalRef.current = p; return p; }
        catch { return prevGlobalRef.current; }
    }, [globalSettingsRaw, forceRender]);

    const prevLocalRef = useRef<LayerSetting[]>([]);
    const localSettings: LayerSetting[] = React.useMemo(() => {
        if (isDraggingRef.current) return prevLocalRef.current;
        try { const p = JSON.parse(localSettingsRaw); prevLocalRef.current = p; return p; }
        catch { return prevLocalRef.current; }
    }, [localSettingsRaw, forceRender]);

    const coverageRange      = useValue(coverageRange$);
    const coverageMagnitude  = useValue(coverageMagnitude$);
    const modifierRange      = useValue(modifierRange$);
    const modifierMagnitude  = useValue(modifierMagnitude$);
    const showStats            = useValue(showStats$);
    const showOnHover           = useValue(showOnHover$);
    const enablePreplacement    = useValue(enablePreplacement$);
    const enableMiniInspector   = useValue(enableMiniInspector$);
    const highVis              = useValue(highVis$);
    const maxDistance          = useValue(maxDistance$);
    const preset               = useValue(preset$);
    const setPreset            = useCallback((v: number) => trigger('area_of_effect', 'setPreset', v), []);

    const presetsRaw           = useValue(presets$);
    const windowX              = useValue(windowX$);
    const windowY              = useValue(windowY$);
    const buildingEffectsRaw   = useValue(buildingEffects$);

    const presets = React.useMemo(() => {
        try { return JSON.parse(presetsRaw); }
        catch {
            return Array.from({ length: 5 }, (_, i) => ({ slot: i, name: `Preset ${i + 1}`, isFilled: false }));
        }
    }, [presetsRaw]);

    const buildingEffects: BuildingEffect[] = React.useMemo(() => {
        try { return JSON.parse(buildingEffectsRaw); }
        catch { return []; }
    }, [buildingEffectsRaw]);

    // Auto-switch to 'Selected Building' tab when a building is selected,
    // and back to 'Layers' when deselected (if they were on the 'selected' tab).
    const [lastSeenBuilding, setLastSeenBuilding] = useState(buildingName);
    React.useEffect(() => {
        const isNewSelection = buildingName && buildingName !== 'No Building Selected' && buildingName !== '' && buildingName !== lastSeenBuilding;
        const isDeselection = (!buildingName || buildingName === 'No Building Selected' || buildingName === '') && lastSeenBuilding && lastSeenBuilding !== 'No Building Selected' && lastSeenBuilding !== '';

        if (isNewSelection) {
            setActiveTab('selected');
        } else if (isDeselection && activeTab === 'selected') {
            setActiveTab('layers');
        }
        setLastSeenBuilding(buildingName);
    }, [buildingName, activeTab, lastSeenBuilding]);

    const handleDragEnd = useCallback((pos: { x: number; y: number }) => {
        trigger('area_of_effect', 'saveWindowPosition', pos.x, pos.y);
    }, []);

    const globalOpacityRaw = useValue(globalOpacity$);
    const prevOpacity = useRef(globalOpacityRaw);
    const globalOpacity = React.useMemo(() => {
        if (!isDraggingRef.current) prevOpacity.current = globalOpacityRaw;
        return prevOpacity.current;
    }, [globalOpacityRaw, forceRender]);

    const globalSizeRaw = useValue(globalSize$);
    const prevSize = useRef(globalSizeRaw);
    const globalSize = React.useMemo(() => {
        if (!isDraggingRef.current) prevSize.current = globalSizeRaw;
        return prevSize.current;
    }, [globalSizeRaw, forceRender]);

    const globalHeightRaw = useValue(globalHeight$);
    const prevHeight = useRef(globalHeightRaw);
    const globalHeight = React.useMemo(() => {
        if (!isDraggingRef.current) prevHeight.current = globalHeightRaw;
        return prevHeight.current;
    }, [globalHeightRaw, forceRender]);

    const bubbleSizeRaw = useValue(bubbleSize$);
    const prevBubble = useRef(bubbleSizeRaw);
    const bubbleSize = React.useMemo(() => {
        if (!isDraggingRef.current) prevBubble.current = bubbleSizeRaw;
        return prevBubble.current;
    }, [bubbleSizeRaw, forceRender]);

    // ?? Stable callbacks ??????????????????????????????????????????????????
    const localColorTimeouts  = useRef<Record<string, any>>({});
    const globalColorTimeouts = useRef<Record<string, any>>({});

    const handleLocalToggle  = useCallback((id: string, enabled: boolean) =>
        trigger('area_of_effect', 'toggleLocalEffect', id, !enabled), []);
    const handleLocalAlpha   = useCallback((id: string, val: number) => {
        markDragging(); trigger('area_of_effect', 'setLocalEffectAlpha', id, val / 100);
    }, [markDragging]);
    const handleLocalColor   = useCallback((id: string, hex: string) => {
        markDragging();
        clearTimeout(localColorTimeouts.current[id]);
        localColorTimeouts.current[id] = setTimeout(() =>
            trigger('area_of_effect', 'setLocalEffectColor', id, hex), 120);
    }, [markDragging]);

    const handleGlobalToggle = useCallback((id: string, enabled: boolean) =>
        trigger('area_of_effect', 'toggleGlobalLayer', id, !enabled), []);
    const handleGlobalAlpha  = useCallback((id: string, val: number) => {
        markDragging(); trigger('area_of_effect', 'setGlobalLayerAlpha', id, val / 100);
    }, [markDragging]);
    const handleGlobalColor  = useCallback((id: string, hex: string) => {
        markDragging();
        clearTimeout(globalColorTimeouts.current[id]);
        globalColorTimeouts.current[id] = setTimeout(() =>
            trigger('area_of_effect', 'setGlobalLayerColor', id, hex), 120);
    }, [markDragging]);

    const handleSetOpacity       = useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setOpacity',    v); }, [markDragging]);
    const handleSetCircleSize    = useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setSize',       v); }, [markDragging]);
    const handleSetOverlayHeight = useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setHeight',     v); }, [markDragging]);
    const handleSetBubbleSize    = useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setBubbleSize', v); }, [markDragging]);

    // ?? Guard ?????????????????????????????????????????????????????????????
    if (!VanillaComponentResolver.instance) {
        return <div className={styles.window}>Loading...</div>;
    }

    const allGlobalEnabled = globalSettings.length > 0 && globalSettings.every(l => l.enabled);
    const allLocalEnabled  = localSettings.length  > 0 && localSettings.every(l => l.enabled);

    const toggleAllGlobal = () => { const s = !allGlobalEnabled; globalSettings.forEach(l => trigger('area_of_effect', 'toggleGlobalLayer',  l.id, s)); };
    const toggleAllLocal  = () => { const s = !allLocalEnabled;  localSettings.forEach( l => trigger('area_of_effect', 'toggleLocalEffect', l.id, s)); };

    // =========================================================================
    //  SELECTED BUILDING TAB
    // =========================================================================
    const renderSelectedTab = () => (
        <div className={styles.section}>
            <div className={styles.infoCard}>
                {(!buildingName || buildingName === 'No Building Selected') ? (
                    <div className={styles.emptyState}>
                        <Icon name="Building" color="rgba(255,255,255,0.15)" size={40} />
                        <div className={styles.emptyStateText}>
                            Select a building in the city to view its Area of Effect stats and active modifiers.
                        </div>
                    </div>
                ) : (
                    <>
                        <div className={styles.selectedBuildingHeader}>
                            <Icon name="Building" color="#00A2E8" size={22} />
                            <div className={styles.selectedBuildingTitle}>{buildingName}</div>
                        </div>
                        {buildingEffects.length === 0 ? (
                            <div className={styles.noEffectsRow}>
                                <div className={styles.noEffectsText}>
                                    No active area of effect stats detected.
                                </div>
                            </div>
                        ) : (
                            <div className={styles.effectsGrid}>
                                {buildingEffects.map((eff, index) => (
                                    <BuildingEffectRow key={index} effect={eff} />
                                ))}
                            </div>
                        )}
                    </>
                )}
            </div>
        </div>
    );

    // =========================================================================
    //  LAYERS TAB
    // =========================================================================
    const renderLayersTab = () => (
        <>
            {/* Visualization Mode Toggle */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <VisualizationModeToggle />
                </div>
            </div>

            {/* Local Effects */}
            {localSettings.length > 0 && (
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <SectionHeader
                            title="Local Effects" collapsed={localCollapsed}
                            onCollapse={() => setLocalCollapsed(v => !v)}
                            allEnabled={allLocalEnabled} onToggleAll={toggleAllLocal}
                        />
                        {!localCollapsed && localSettings
                            .filter(l => l.id === 'CoverageData' || l.id === 'LocalModifier_Wellbeing' || l.id === 'PreplacementRing')
                            .map(layer => (
                            <LayerRow key={layer.id} layer={layer}
                                onToggle={handleLocalToggle} onAlpha={handleLocalAlpha} onColor={handleLocalColor} />
                        ))}
                    </div>
                </div>
            )}

            {/* Global Layers */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <SectionHeader
                        title="Global Layers" collapsed={globalCollapsed}
                        onCollapse={() => setGlobalCollapsed(v => !v)}
                        allEnabled={allGlobalEnabled} onToggleAll={toggleAllGlobal}
                    />
                    {!globalCollapsed && globalSettings
                        .filter(l => l.id !== 'layer_pollution')
                        .map(layer => (
                        <LayerRow key={layer.id} layer={layer}
                            onToggle={handleGlobalToggle} onAlpha={handleGlobalAlpha} onColor={handleGlobalColor} />
                    ))}
                </div>
            </div>
        </>
    );

    // =========================================================================
    //  SETTINGS TAB
    // =========================================================================
    const renderSettingsTab = () => (
        <>
            {/* Visual Config */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <div className={styles.cardHeader}>
                        <div className={styles.sectionTitle}>Visual Config</div>
                    </div>
                    <SettingsSliderRow label="Global Opacity" value={globalOpacity} min={0}  max={100} unit=" %" onChange={handleSetOpacity} />
                    <SettingsSliderRow label="Global Radius"  value={globalSize}    min={10} max={200} unit=" m" onChange={handleSetCircleSize} />
                    <SettingsSliderRow label="Global Height"  value={globalHeight}  min={0}  max={100} unit=" m" onChange={handleSetOverlayHeight} />
                    <div className={styles.layerRowNative} style={{ marginTop: '12rem', marginBottom: '4rem' }}>
                        <div className={styles.statRowLabel} style={{ flexGrow: 1 }}>Visual Preset</div>
                        <div className={styles.presetButtons}>
                            <button className={`${styles.footerBtn} ${preset === 0 ? styles.footerBtnPrimary : ''}`} onClick={() => setPreset(0)}>Neon Rings</button>
                            <button className={`${styles.footerBtn} ${preset === 1 ? styles.footerBtnPrimary : ''}`} onClick={() => setPreset(1)}>Soft Glow</button>
                            <button className={`${styles.footerBtn} ${preset === 2 ? styles.footerBtnPrimary : ''}`} onClick={() => setPreset(2)}>Classic</button>
                        </div>
                    </div>
                </div>
            </div>

            {/* Display Options */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <div className={styles.cardHeader}>
                        <div className={styles.sectionTitle}>Display Options</div>
                    </div>
                    <SettingsToggleRow label="Display Stat Bubbles"         checked={showStats}              onChange={() => trigger('area_of_effect', 'setShowStats',              !showStats)} />
                    <SettingsToggleRow label="Show Overlay on Hover"         checked={showOnHover}            onChange={() => trigger('area_of_effect', 'setShowOnHover',            !showOnHover)} />
                    <SettingsToggleRow label="Enable Pre-placement Ring"     checked={enablePreplacement}     onChange={() => trigger('area_of_effect', 'setEnablePreplacement',     !enablePreplacement)} />
                    <SettingsToggleRow label="Enable Mini-Inspector"        checked={enableMiniInspector}    onChange={() => trigger('area_of_effect', 'setEnableMiniInspector',    !enableMiniInspector)} />
                    <SettingsToggleRow label="High Visibility Mode"          checked={highVis}                onChange={() => trigger('area_of_effect', 'setHighVis',               !highVis)} />
                    <SettingsSliderRow label="Max Label Distance" value={maxDistance} min={1500} max={3600} unit=" m" onChange={v => trigger('area_of_effect', 'setMaxDistance', v)} />
                    <SettingsSliderRow label="Map Bubble Size"    value={bubbleSize}  min={50}   max={200}  unit=" %" onChange={handleSetBubbleSize} />
                </div>
            </div>


        </>
    );

    // =========================================================================
    //  PRESETS TAB
    // =========================================================================
    const renderPresetsTab = () => (
        <div className={styles.section}>
            <div className={styles.infoCard}>
                <div className={styles.cardHeader}>
                    <div className={styles.sectionTitle}>Saved Configurations</div>
                </div>
                {presets.map((preset: { slot: number; name: string; isFilled: boolean }) => (
                    <div key={preset.slot} className={styles.layerRowNative} style={{ justifyContent: 'space-between', padding: '10rem 0', borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
                            <div style={{ color: preset.isFilled ? '#FFF' : '#888', fontWeight: preset.isFilled ? 700 : 400, fontSize: '14rem' }}>
                                {preset.name}
                            </div>
                            <div style={{ fontSize: '11rem', color: preset.isFilled ? '#8a9ba8' : '#4f5d6a' }}>
                                {preset.isFilled ? 'Slot Filled' : 'Empty Slot'}
                            </div>
                        </div>
                        <div style={{ display: 'flex' }}>
                            {preset.isFilled && (
                                <>
                                    <button className={styles.footerBtn} style={{ padding: '4rem 10rem', minWidth: '60rem', marginRight: '8rem' }} onClick={() => trigger('area_of_effect', 'loadPreset', preset.slot)}>
                                        Load
                                    </button>
                                    <button className={styles.footerBtn} style={{ padding: '4rem 10rem', minWidth: '60rem', color: '#ff4d4d', border: '1px solid rgba(255,77,77,0.3)', marginRight: '8rem' }} onClick={() => setDeletePresetSlot(preset.slot)}>
                                        Delete
                                    </button>
                                </>
                            )}
                            <button className={styles.footerBtnPrimary} style={{ padding: '4rem 10rem', minWidth: '60rem' }} onClick={() => {
                                setSelectedPresetSlot(preset.slot);
                                setPresetNameInput(preset.isFilled ? preset.name : `Preset ${preset.slot + 1}`);
                                setShowSavePresetModal(true);
                            }}>
                                {preset.isFilled ? 'Overwrite' : 'Save'}
                            </button>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );

    // =========================================================================
    //  RENDER
    // =========================================================================
    return (
        <div className={styles.fullScreenWrapper}>
            <DraggableWindow initialX={windowX} initialY={windowY} id="aoe_panel" onDragEnd={handleDragEnd}>
                <div className={styles.window}>
                    <PanelHeader>
                        <div className={styles.studioLabel}>
                            <Icon name="Building" color="#FFFFFF" size={24} />
                            <div className={styles.buildingName}>Area of Effect</div>
                        </div>
                        <div
                            onClick={() => trigger('area_of_effect', 'togglePanel')}
                            onMouseDown={e => e.stopPropagation()}
                            style={{ padding: '4rem 10rem', cursor: 'pointer', display: 'flex', alignItems: 'center' }}
                        >
                            <svg width="24" height="24" viewBox="0 0 24 24" fill="none"
                                stroke="#FFFFFF" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <line x1="18" y1="6"  x2="6"  y2="18" />
                                <line x1="6"  y1="6"  x2="18" y2="18" />
                            </svg>
                        </div>
                    </PanelHeader>

                    <div className={styles.tabBar}>
                        <div className={`${styles.tabBtn} ${activeTab === 'selected' ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('selected')}>Selected Building</div>
                        <div className={`${styles.tabBtn} ${activeTab === 'layers'   ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('layers')}>Layers</div>
                        <div className={`${styles.tabBtn} ${activeTab === 'settings' ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('settings')}>Settings</div>
                        <div className={`${styles.tabBtn} ${activeTab === 'presets'  ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('presets')}>Presets</div>
                    </div>

                    <ScrollBar>
                        {activeTab === 'selected' ? renderSelectedTab() :
                         activeTab === 'layers' ? renderLayersTab() :
                         activeTab === 'settings' ? renderSettingsTab() :
                         renderPresetsTab()}
                    </ScrollBar>

                    {activeTab === 'layers' && (
                        <div className={styles.footerActions} style={{ justifyContent: 'center' }}>
                            <div className={styles.footerBtn} onClick={() => { setLocalCollapsed(true); setGlobalCollapsed(true); }} style={{ margin: 0 }}>Collapse All</div>
                        </div>
                    )}

                    {activeTab === 'settings' && (
                        <div className={styles.footerActions} style={{ justifyContent: 'center' }}>
                            <button className={styles.footerBtn} style={{ margin: '0 5rem' }} onClick={() => { setResetType('colors'); setShowResetModal(true); }}>Reset Colors Only</button>
                            <button className={styles.footerBtn} style={{ margin: '0 5rem' }} onClick={() => { setResetType('config'); setShowResetModal(true); }}>Reset Configuration (Keep Colors)</button>
                        </div>
                    )}

                    {showResetModal && (
                        <div style={{
                            position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
                            backgroundColor: 'rgba(0,0,0,0.8)', display: 'flex', 
                            alignItems: 'center', justifyContent: 'center', zIndex: 100
                        }}>
                            <div style={{
                                backgroundColor: '#1a2025', padding: '20rem', borderRadius: '8rem',
                                border: '1px solid rgba(255,255,255,0.2)', textAlign: 'center',
                                width: '280rem'
                            }}>
                                <h3 style={{ color: 'white', marginTop: 0 }}>
                                    {resetType === 'colors' ? 'Reset Colors?' : 'Reset Configuration?'}
                                </h3>
                                <p style={{ color: 'rgba(255,255,255,0.7)', marginBottom: '20rem', fontSize: '12rem' }}>
                                    {resetType === 'colors'
                                        ? 'This will restore all default local layer colors and opacities. Active toggles will not be changed.'
                                        : 'This will restore all default configuration sliders (opacity, radius, etc.) and window coordinates. Layer colors will not be changed.'}
                                </p>
                                <div style={{ display: 'flex', justifyContent: 'center' }}>
                                    <button className={styles.footerBtn} style={{ margin: '0 5rem' }} onClick={() => setShowResetModal(false)}>Cancel</button>
                                    <button className={styles.footerBtnPrimary} style={{ margin: '0 5rem' }} onClick={() => {
                                        if (resetType === 'colors') {
                                            trigger('area_of_effect', 'resetColorsOnly');
                                        } else {
                                            trigger('area_of_effect', 'resetConfigOnly');
                                        }
                                        setShowResetModal(false);
                                    }}>Confirm Reset</button>
                                </div>
                            </div>
                        </div>
                    )}

                    {showSavePresetModal && selectedPresetSlot !== null && (
                        <div style={{
                            position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
                            backgroundColor: 'rgba(0,0,0,0.8)', display: 'flex', 
                            alignItems: 'center', justifyContent: 'center', zIndex: 100
                        }}>
                            <div style={{
                                backgroundColor: '#1a2025', padding: '20rem', borderRadius: '8rem',
                                border: '1px solid rgba(255,255,255,0.2)', textAlign: 'center',
                                width: '280rem'
                            }}>
                                <h3 style={{ color: 'white', marginTop: 0 }}>Save Configuration</h3>
                                <p style={{ color: 'rgba(255,255,255,0.7)', marginBottom: '10rem', fontSize: '14rem' }}>
                                    {presets[selectedPresetSlot].isFilled 
                                        ? 'This slot is already filled. Overwriting will replace the saved configuration.' 
                                        : 'Type a name for this configuration preset:'}
                                </p>
                                <input
                                    type="text"
                                    value={presetNameInput}
                                    onChange={(e) => setPresetNameInput(e.target.value)}
                                    maxLength={25}
                                    style={{
                                        width: '100%',
                                        padding: '8rem',
                                        backgroundColor: '#0a0d0f',
                                        border: '1px solid rgba(255,255,255,0.1)',
                                        color: '#fff',
                                        borderRadius: '4rem',
                                        fontSize: '16rem',
                                        marginBottom: '20rem',
                                        outline: 'none'
                                    }}
                                    placeholder="Preset Name"
                                    onMouseDown={(e) => e.stopPropagation()} // Prevent drag handle triggers
                                />
                                <div style={{ display: 'flex', justifyContent: 'center' }}>
                                    <button className={styles.footerBtn} style={{ margin: '0 5rem' }} onClick={() => setShowSavePresetModal(false)}>Cancel</button>
                                    <button className={styles.footerBtnPrimary} style={{ margin: '0 5rem' }} onClick={() => {
                                        trigger('area_of_effect', 'savePreset', selectedPresetSlot, presetNameInput || `Preset ${selectedPresetSlot + 1}`);
                                        setShowSavePresetModal(false);
                                    }}>
                                        {presets[selectedPresetSlot].isFilled ? 'Confirm Overwrite' : 'Save'}
                                    </button>
                                </div>
                            </div>
                        </div>
                    )}

                    {deletePresetSlot !== null && (
                        <div style={{
                            position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
                            backgroundColor: 'rgba(0,0,0,0.8)', display: 'flex', 
                            alignItems: 'center', justifyContent: 'center', zIndex: 100
                        }}>
                            <div style={{
                                backgroundColor: '#1a2025', padding: '20rem', borderRadius: '8rem',
                                border: '1px solid rgba(255,255,255,0.2)', textAlign: 'center',
                                width: '280rem'
                            }}>
                                <h3 style={{ color: 'white', marginTop: 0 }}>Delete Preset</h3>
                                <p style={{ color: 'rgba(255,255,255,0.7)', marginBottom: '20rem', fontSize: '12rem' }}>
                                    Delete Preset? This will permanently remove this saved configuration.
                                </p>
                                <div style={{ display: 'flex', justifyContent: 'center' }}>
                                    <button className={styles.footerBtn} style={{ margin: '0 5rem' }} onClick={() => setDeletePresetSlot(null)}>Cancel</button>
                                    <button className={styles.footerBtnPrimary} style={{ backgroundColor: '#ff4d4d', margin: '0 5rem' }} onClick={() => {
                                        trigger('area_of_effect', 'deletePreset', deletePresetSlot);
                                        setDeletePresetSlot(null);
                                    }}>Delete</button>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </DraggableWindow>
        </div>
    );
};
