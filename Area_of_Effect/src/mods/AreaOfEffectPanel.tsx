import React, { useState, useRef, useCallback } from 'react';
import { bindValue, useValue, trigger } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';
import { Icon } from './Icons';
import { DraggableWindow, DragContext } from './DraggableWindow';
import { VanillaComponentResolver } from './VanillaComponents';

// ─── PanelHeader: ONLY element that gets the drag onMouseDown ────────────────
const PanelHeader: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const onDrag = React.useContext(DragContext);
    return <div className={styles.header} onMouseDown={onDrag ?? undefined}>{children}</div>;
};

// ─── Layer Data Shape (strictly lowercase to match C# JSON serialization) ────
interface LayerSetting {
    id: string;
    name: string;
    enabled: boolean;
    opacity: number;
    r: number;
    g: number;
    b: number;
    a: number;
}

// ─── Custom Toggle: CSS-only, guaranteed to render in Cohtml ─────────────────
const AoeToggle: React.FC<{ checked: boolean; onChange: () => void }> = ({ checked, onChange }) => (
    <div
        className={`${styles.toggleSwitch} ${checked ? styles.toggleSwitchOn : ''}`}
        onClick={(e) => { e.stopPropagation(); onChange(); }}
    >
        <div className={`${styles.toggleThumb} ${checked ? styles.toggleThumbOn : ''}`} />
    </div>
);

// ─── Helper: convert layer color floats to hex for Cohtml ────────────────────
function layerToHex(layer: LayerSetting): string {
    if (typeof layer.r !== 'number' || isNaN(layer.r) || 
        typeof layer.g !== 'number' || isNaN(layer.g) || 
        typeof layer.b !== 'number' || isNaN(layer.b)) {
        return '#ffffff';
    }
    const toHex = (v: number) => Math.round(v * 255).toString(16).padStart(2, '0');
    return `#${toHex(layer.r)}${toHex(layer.g)}${toHex(layer.b)}`;
}

// ─── Bindings ────────────────────────────────────────────────────────────────
const globalSettings$ = bindValue<string>('area_of_effect', 'globalSettings', '[]');
const localSettings$ = bindValue<string>('area_of_effect', 'localSettings', '[]');

const buildingName$ = bindValue<string>('area_of_effect', 'buildingName', 'No Building Selected');
const buildingEfficiency$ = bindValue<number>('area_of_effect', 'buildingEfficiency', 0);
const buildingWellbeing$ = bindValue<number>('area_of_effect', 'buildingWellbeing', 0);
const buildingServiceReach$ = bindValue<number>('area_of_effect', 'buildingServiceReach', 0);

const showStats$ = bindValue<boolean>('area_of_effect', 'showStats', true);
const highVis$ = bindValue<boolean>('area_of_effect', 'highVis', false);
const maxDistance$ = bindValue<number>('area_of_effect', 'maxDistance', 1000);

const globalOpacity$ = bindValue<number>('area_of_effect', 'opacity', 100);
const globalSize$ = bindValue<number>('area_of_effect', 'circleSize', 500);
const globalHeight$ = bindValue<number>('area_of_effect', 'overlayHeight', 0);
const preset$ = bindValue<number>('area_of_effect', 'preset', 0);

// ─── Main Panel Component ────────────────────────────────────────────────────
export const AreaOfEffectPanel: React.FC = () => {
    const [activeTab, setActiveTab] = useState<'layers' | 'settings'>('layers');
    const [localCollapsed, setLocalCollapsed] = useState(false);
    const [globalCollapsed, setGlobalCollapsed] = useState(false);

    // ── Interaction Freeze Logic ─────────────────────────────────────────
    const isDraggingRef = useRef(false);
    const dragTimeoutRef = useRef<any>(null);
    const [forceRender, setForceRender] = useState(0);

    const markDragging = useCallback(() => {
        isDraggingRef.current = true;
        if (dragTimeoutRef.current) clearTimeout(dragTimeoutRef.current);
        dragTimeoutRef.current = setTimeout(() => {
            isDraggingRef.current = false;
            setForceRender(v => v + 1); // unfreeze UI
        }, 500);
    }, []);

    // ── Bindings ─────────────────────────────────────────────────────────
    const globalSettingsRaw = useValue(globalSettings$);
    const localSettingsRaw = useValue(localSettings$);

    const prevGlobalRef = useRef<LayerSetting[]>([]);
    const globalSettings: LayerSetting[] = React.useMemo(() => {
        if (isDraggingRef.current) return prevGlobalRef.current;
        try { 
            const parsed = JSON.parse(globalSettingsRaw); 
            prevGlobalRef.current = parsed;
            return parsed;
        } catch { return prevGlobalRef.current; }
    }, [globalSettingsRaw, forceRender]);

    const prevLocalRef = useRef<LayerSetting[]>([]);
    const localSettings: LayerSetting[] = React.useMemo(() => {
        if (isDraggingRef.current) return prevLocalRef.current;
        try { 
            const parsed = JSON.parse(localSettingsRaw); 
            prevLocalRef.current = parsed;
            return parsed;
        } catch { return prevLocalRef.current; }
    }, [localSettingsRaw, forceRender]);

    const buildingName = useValue(buildingName$);
    const buildingWellbeing = useValue(buildingWellbeing$);
    const buildingServiceReach = useValue(buildingServiceReach$);

    const showStats = useValue(showStats$);
    const highVis = useValue(highVis$);
    const maxDistance = useValue(maxDistance$);

    const globalOpacityRaw = useValue(globalOpacity$);
    const prevGlobalOpacity = useRef(globalOpacityRaw);
    const globalOpacity = React.useMemo(() => {
        if (!isDraggingRef.current) prevGlobalOpacity.current = globalOpacityRaw;
        return prevGlobalOpacity.current;
    }, [globalOpacityRaw, forceRender]);

    const globalSizeRaw = useValue(globalSize$);
    const prevGlobalSize = useRef(globalSizeRaw);
    const globalSize = React.useMemo(() => {
        if (!isDraggingRef.current) prevGlobalSize.current = globalSizeRaw;
        return prevGlobalSize.current;
    }, [globalSizeRaw, forceRender]);

    const globalHeightRaw = useValue(globalHeight$);
    const prevGlobalHeight = useRef(globalHeightRaw);
    const globalHeight = React.useMemo(() => {
        if (!isDraggingRef.current) prevGlobalHeight.current = globalHeightRaw;
        return prevGlobalHeight.current;
    }, [globalHeightRaw, forceRender]);

    // ── Guard: wait for native component registry ────────────────────────
    if (!VanillaComponentResolver.instance) {
        return <div className={styles.window}>Loading...</div>;
    }

    const { Slider, ColorField } = VanillaComponentResolver.instance;

    // ── Master toggle helpers ────────────────────────────────────────────
    const allGlobalEnabled = globalSettings.length > 0 && globalSettings.every(l => l.enabled);
    const allLocalEnabled = localSettings.length > 0 && localSettings.every(l => l.enabled);

    const toggleAllGlobal = () => {
        const newState = !allGlobalEnabled;
        globalSettings.forEach(l => trigger('area_of_effect', 'toggleGlobalLayer', l.id, newState));
    };
    const toggleAllLocal = () => {
        const newState = !allLocalEnabled;
        localSettings.forEach(l => trigger('area_of_effect', 'toggleLocalEffect', l.id, newState));
    };

    // Stable Callbacks to prevent LayerRow re-renders
    const handleLocalToggle = useCallback((id: string, enabled: boolean) => trigger('area_of_effect', 'toggleLocalEffect', id, !enabled), []);
    const handleLocalAlpha = useCallback((id: string, val: number) => { markDragging(); trigger('area_of_effect', 'setLocalEffectAlpha', id, val / 100); }, [markDragging]);
    const handleLocalColor = useCallback((id: string, hex: string) => { markDragging(); trigger('area_of_effect', 'setLocalEffectColor', id, hex); }, [markDragging]);

    const handleGlobalToggle = useCallback((id: string, enabled: boolean) => trigger('area_of_effect', 'toggleGlobalLayer', id, !enabled), []);
    const handleGlobalAlpha = useCallback((id: string, val: number) => { markDragging(); trigger('area_of_effect', 'setGlobalLayerAlpha', id, val / 100); }, [markDragging]);
    const handleGlobalColor = useCallback((id: string, hex: string) => { markDragging(); trigger('area_of_effect', 'setGlobalLayerColor', id, hex); }, [markDragging]);

    // ── Layer Row ────────────────────────────────────────────────────────
    const LayerRow = React.memo<{
        layer: LayerSetting;
        onToggle: (id: string, enabled: boolean) => void;
        onAlpha: (id: string, val: number) => void;
        onColor: (id: string, hex: string) => void;
    }>(({ layer, onToggle, onAlpha, onColor }) => {
        const color = layerToHex(layer);
        
        // Controlled locally so it moves instantly, but C# data is sent instantly too
        const [displayColor, setDisplayColor] = useState({ r: layer.r, g: layer.g, b: layer.b, a: layer.a });
        const [displayOpacity, setDisplayOpacity] = useState((layer.opacity || 0) * 100);

        React.useEffect(() => {
            setDisplayColor({ r: layer.r, g: layer.g, b: layer.b, a: layer.a });
            setDisplayOpacity((layer.opacity || 0) * 100);
        }, [layer]);

        const handleColorChange = useCallback((newColor: { r: number; g: number; b: number; a: number }) => {
            setDisplayColor(newColor);
            const toHex = (v: number) => Math.round(v * 255).toString(16).padStart(2, '0');
            const hex = `#${toHex(newColor.r)}${toHex(newColor.g)}${toHex(newColor.b)}`;
            onColor(layer.id, hex);
        }, [layer.id, onColor]);

        const handleAlphaChange = useCallback((val: number) => {
            setDisplayOpacity(val);
            onAlpha(layer.id, val);
        }, [layer.id, onAlpha]);

        return (
            <div style={{ marginBottom: '8rem', paddingBottom: '6rem', borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
                {/* Row 1: Toggle + Icon + Clickable Name (with invisible ColorField overlay) */}
                <div className={styles.layerRowNative}>
                    <AoeToggle checked={layer.enabled} onChange={() => onToggle(layer.id, layer.enabled)} />
                    <div className={styles.layerIconWrapNative}>
                        <Icon name={layer.id} color={color} size={22} />
                    </div>
                    
                    <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                        <div className={styles.bracketLabelNative} style={{ color }}>
                            {layer.name}
                        </div>
                        {/* Invisible ColorField that intercepts the click and opens the picker */}
                        <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, opacity: 0, overflow: 'hidden', cursor: 'pointer' }}>
                            <ColorField
                                value={displayColor}
                                onChange={handleColorChange}
                            />
                        </div>
                    </div>
                </div>
                {/* Row 2: Opacity slider */}
                <div className={styles.layerRowNative} style={{ paddingLeft: '52rem' }}>
                    <span className={styles.statRowLabel}>Opacity</span>
                    <div className={styles.sliderContainerNative}>
                        <div className={styles.sliderWrap}>
                            <Slider value={displayOpacity} start={0} end={100} onChange={handleAlphaChange} />
                        </div>
                        <span className={styles.sliderValText}>{Math.round(displayOpacity)}%</span>
                    </div>
                </div>
            </div>
        );
    });

    // ── Collapsible Section Header ───────────────────────────────────────
    const SectionHeader: React.FC<{
        title: string;
        collapsed: boolean;
        onCollapse: () => void;
        allEnabled?: boolean;
        onToggleAll?: () => void;
    }> = ({ title, collapsed, onCollapse, allEnabled, onToggleAll }) => (
        <div className={styles.cardHeader} style={{ cursor: 'pointer', userSelect: 'none' }}>
            <span className={styles.sectionTitle} onClick={onCollapse} style={{ flexGrow: 1 }}>
                {collapsed ? '▶' : '▼'} {title}
            </span>
            {onToggleAll !== undefined && allEnabled !== undefined && (
                <AoeToggle checked={allEnabled} onChange={onToggleAll} />
            )}
        </div>
    );

    // ── Settings Row helpers ─────────────────────────────────────────────
    const SettingsSliderRow = React.memo<{
        label: string; value: number; min: number; max: number; unit: string; onChange: (v: number) => void;
    }>(({ label, value, min, max, unit, onChange }) => {
        const [displayVal, setDisplayVal] = useState(value);

        React.useEffect(() => {
            setDisplayVal(value);
        }, [value]);

        const handleSliderChange = useCallback((val: number) => {
            setDisplayVal(val);
            onChange(val);
        }, [onChange]);

        return (
            <div className={styles.layerRowNative} style={{ marginBottom: '6rem' }}>
                <span className={styles.statRowLabel} style={{ width: '120rem', minWidth: '120rem' }}>{label}</span>
                <div className={styles.sliderContainerNative}>
                    <div className={styles.sliderWrap}>
                        <Slider value={displayVal} start={min} end={max} onChange={handleSliderChange} />
                    </div>
                    <span className={styles.sliderValText}>{Math.round(displayVal)}{unit}</span>
                </div>
            </div>
        );
    });

    const SettingsToggleRow: React.FC<{
        label: string; checked: boolean; onChange: () => void;
    }> = ({ label, checked, onChange }) => (
        <div className={styles.layerRowNative} style={{ marginBottom: '8rem' }}>
            <span className={styles.statRowLabel} style={{ flexGrow: 1 }}>{label}</span>
            <AoeToggle checked={checked} onChange={onChange} />
        </div>
    );

    // ── LAYERS TAB ───────────────────────────────────────────────────────
    const renderLayersTab = () => (
        <>
            {/* Selected Building Stats */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <div className={styles.cardHeader}>
                        <span className={styles.sectionTitle}>Selected Building</span>
                    </div>
                    {(!buildingName || buildingName === 'No Building Selected') ? (
                        <div className={styles.layerRowNative}>
                            <span className={styles.statRowLabel} style={{ fontStyle: 'italic', color: '#888' }}>
                                Select a building to view its effects
                            </span>
                        </div>
                    ) : (
                        <>
                            <div style={{ fontSize: '15rem', fontWeight: 700, color: '#FFF', marginBottom: '8rem' }}>{buildingName}</div>
                            <div className={styles.layerRowNative}>
                                <span className={styles.statRowLabel} style={{ width: '120rem' }}>Well-being</span>
                                <span className={styles.statRowValue}>{buildingWellbeing > 0 ? '+' : ''}{Math.round(buildingWellbeing)}%</span>
                            </div>
                            <div className={styles.layerRowNative}>
                                <span className={styles.statRowLabel} style={{ width: '120rem' }}>Service Reach</span>
                                <span className={styles.statRowValue}>{Math.round(buildingServiceReach)}m</span>
                            </div>
                        </>
                    )}
                </div>
            </div>

            {/* Local Effects (above Global per user request) */}
            {localSettings.length > 0 && (
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <SectionHeader
                            title="Local Effects"
                            collapsed={localCollapsed}
                            onCollapse={() => setLocalCollapsed(!localCollapsed)}
                            allEnabled={allLocalEnabled}
                            onToggleAll={toggleAllLocal}
                        />
                        {!localCollapsed && localSettings.map((layer) => (
                            <LayerRow
                                key={layer.id}
                                layer={layer}
                                onToggle={handleLocalToggle}
                                onAlpha={handleLocalAlpha}
                                onColor={handleLocalColor}
                            />
                        ))}
                    </div>
                </div>
            )}

            {/* Global Layers */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <SectionHeader
                        title="Global Layers"
                        collapsed={globalCollapsed}
                        onCollapse={() => setGlobalCollapsed(!globalCollapsed)}
                        allEnabled={allGlobalEnabled}
                        onToggleAll={toggleAllGlobal}
                    />
                    {!globalCollapsed && globalSettings.map((layer) => (
                        <LayerRow
                            key={layer.id}
                            layer={layer}
                            onToggle={handleGlobalToggle}
                            onAlpha={handleGlobalAlpha}
                            onColor={handleGlobalColor}
                        />
                    ))}
                </div>
            </div>
        </>
    );

    // ── SETTINGS TAB ─────────────────────────────────────────────────────
    const renderSettingsTab = () => (
        <>
            {/* Visual Config */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <div className={styles.cardHeader}>
                        <span className={styles.sectionTitle}>Visual Config</span>
                    </div>
                    <SettingsSliderRow 
                        label="Global Opacity" 
                        value={globalOpacity} 
                        min={0} max={100} unit="%" 
                        onChange={useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setOpacity', v); }, [markDragging])} 
                    />
                    <SettingsSliderRow 
                        label="Global Radius" 
                        value={globalSize} 
                        min={10} max={1000} unit="m" 
                        onChange={useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setCircleSize', v); }, [markDragging])} 
                    />
                    <SettingsSliderRow 
                        label="Global Height" 
                        value={globalHeight} 
                        min={-100} max={500} unit="m" 
                        onChange={useCallback((v: number) => { markDragging(); trigger('area_of_effect', 'setOverlayHeight', v); }, [markDragging])} 
                    />
                </div>
            </div>

            {/* Display Options */}
            <div className={styles.section}>
                <div className={styles.infoCard}>
                    <div className={styles.cardHeader}>
                        <span className={styles.sectionTitle}>Display Options</span>
                    </div>
                    <SettingsToggleRow label="Display Stat Bubbles" checked={showStats} onChange={() => trigger('area_of_effect', 'setShowStats', !showStats)} />
                    <SettingsToggleRow label="High Visibility Mode" checked={highVis} onChange={() => trigger('area_of_effect', 'setHighVis', !highVis)} />
                    <SettingsSliderRow label="Max Label Distance" value={maxDistance} min={50} max={5000} unit="m" onChange={(v) => trigger('area_of_effect', 'setMaxDistance', v)} />
                </div>
            </div>


        </>
    );

    // ── Render ────────────────────────────────────────────────────────────
    return (
        <div className={styles.fullScreenWrapper}>
            <DraggableWindow initialX={100} initialY={100} id="aoe_panel">
                <div className={styles.window}>
                    {/* Header — drag handle */}
                    <PanelHeader>
                        <div className={styles.studioLabel}>
                            <Icon name="Building" color="#FFFFFF" size={24} />
                            <span className={styles.buildingName}>Area of Effect</span>
                        </div>
                        <div
                            onClick={() => trigger('area_of_effect', 'togglePanel')}
                            onMouseDown={(e) => e.stopPropagation()}
                            style={{ padding: '4rem 10rem', cursor: 'pointer', display: 'flex', alignItems: 'center' }}
                        >
                            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#FFFFFF" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <line x1="18" y1="6" x2="6" y2="18"></line>
                                <line x1="6" y1="6" x2="18" y2="18"></line>
                            </svg>
                        </div>
                    </PanelHeader>

                    {/* Tab Bar */}
                    <div className={styles.tabBar}>
                        <div className={`${styles.tabBtn} ${activeTab === 'layers' ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('layers')}>
                            Layers
                        </div>
                        <div className={`${styles.tabBtn} ${activeTab === 'settings' ? styles.tabBtnActive : ''}`} onClick={() => setActiveTab('settings')}>
                            Settings
                        </div>
                    </div>

                    {/* Scroll Body — NO stopPropagation, drag is header-only */}
                    <div className={styles.scrollBody}>
                        {activeTab === 'layers' ? renderLayersTab() : renderSettingsTab()}
                    </div>

                    {/* Footer */}
                    <div className={styles.footerActions}>
                        <div className={styles.footerBtn} onClick={() => { setLocalCollapsed(true); setGlobalCollapsed(true); }}>Collapse All</div>
                        <div className={`${styles.footerBtn} ${styles.footerBtnPrimary}`}>Save Preset</div>
                    </div>
                </div>
            </DraggableWindow>
        </div>
    );
};
