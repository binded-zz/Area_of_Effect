import React from 'react';
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

// ─── Event-safe wrapper: stops drag handler from hijacking input ─────────────
const InputGuard: React.FC<{ children: React.ReactNode; style?: React.CSSProperties }> = ({ children, style }) => (
    <div onMouseDown={(e) => e.stopPropagation()} onClick={(e) => e.stopPropagation()} style={style}>
        {children}
    </div>
);

// ─── Bindings ────────────────────────────────────────────────────────────────
const globalSettings$ = bindValue<string>('area_of_effect', 'globalSettings', '[]');
const localSettings$ = bindValue<string>('area_of_effect', 'localSettings', '[]');

const buildingName$ = bindValue<string>('area_of_effect', 'buildingName', 'No Building Selected');
const buildingEfficiency$ = bindValue<number>('area_of_effect', 'buildingEfficiency', 0);
const buildingWellbeing$ = bindValue<number>('area_of_effect', 'buildingWellbeing', 0);
const buildingServiceReach$ = bindValue<number>('area_of_effect', 'buildingServiceReach', 0);

const layoutMode$ = bindValue<number>('area_of_effect', 'layoutMode', 0);
const showStats$ = bindValue<boolean>('area_of_effect', 'showStats', true);
const highVis$ = bindValue<boolean>('area_of_effect', 'highVis', false);
const isLightMode$ = bindValue<boolean>('area_of_effect', 'isLightMode', false);
const maxDistance$ = bindValue<number>('area_of_effect', 'maxDistance', 1000);

// Global visual state bindings
const globalOpacity$ = bindValue<number>('area_of_effect', 'opacity', 100);
const globalSize$ = bindValue<number>('area_of_effect', 'circleSize', 500);
const globalHeight$ = bindValue<number>('area_of_effect', 'overlayHeight', 0);

// ─── Main Panel Component ────────────────────────────────────────────────────
export const AreaOfEffectPanel: React.FC = () => {
    // ── Reactive values ──────────────────────────────────────────────────
    const globalSettingsRaw = useValue(globalSettings$);
    const localSettingsRaw = useValue(localSettings$);

    const globalSettings: LayerSetting[] = React.useMemo(() => {
        try { return JSON.parse(globalSettingsRaw); } catch { return []; }
    }, [globalSettingsRaw]);

    const localSettings: LayerSetting[] = React.useMemo(() => {
        try { return JSON.parse(localSettingsRaw); } catch { return []; }
    }, [localSettingsRaw]);

    const buildingName = useValue(buildingName$);
    const buildingEfficiency = useValue(buildingEfficiency$);
    const buildingWellbeing = useValue(buildingWellbeing$);
    const buildingServiceReach = useValue(buildingServiceReach$);

    const layoutMode = useValue(layoutMode$);
    const showStats = useValue(showStats$);
    const highVis = useValue(highVis$);
    const isLightMode = useValue(isLightMode$);
    const maxDistance = useValue(maxDistance$);

    const globalOpacity = useValue(globalOpacity$);
    const globalSize = useValue(globalSize$);
    const globalHeight = useValue(globalHeight$);

    // ── Triggers (C# backend) ────────────────────────────────────────────
    const toggleGlobalLayer = (id: string, enabled: boolean) => trigger('area_of_effect', 'toggleGlobalLayer', id, !enabled);
    const toggleLocalEffect = (id: string, enabled: boolean) => trigger('area_of_effect', 'toggleLocalEffect', id, !enabled);

    // Global single-float setters
    const setOpacity = (val: number) => trigger('area_of_effect', 'setOpacity', val);
    const setSize = (val: number) => trigger('area_of_effect', 'setSize', val);
    const setHeight = (val: number) => trigger('area_of_effect', 'setHeight', val);

    // Configuration setters
    const setLayoutMode = (val: number) => trigger('area_of_effect', 'setLayoutMode', val);
    const setPreset = (val: number) => trigger('area_of_effect', 'setPreset', val);
    const setMaxDistance = (val: number) => trigger('area_of_effect', 'setMaxDistance', val);

    // Per-row layer setters
    const setGlobalLayerAlpha = (id: string, val: number) => trigger('area_of_effect', 'setGlobalLayerAlpha', id, val / 100);
    const setLocalEffectAlpha = (id: string, val: number) => trigger('area_of_effect', 'setLocalEffectAlpha', id, val / 100);

    const toggleShowStats = () => trigger('area_of_effect', 'setShowStats', !showStats);
    const toggleHighVis = () => trigger('area_of_effect', 'setHighVis', !highVis);

    // ── Guard: wait for native component registry ────────────────────────
    if (!VanillaComponentResolver.instance) {
        return <div className={styles.window}>Loading Native Components...</div>;
    }

    const { Slider, Toggle, Scrollable, Panel } = VanillaComponentResolver.instance;
    const ScrollContainer = Scrollable || (({ children, className }: any) => <div className={className}>{children}</div>);

    // ── Build the panel content ──────────────────────────────────────────
    const panelContent = (
        <Panel className={`${styles.window} ${isLightMode ? styles.lightTheme : ''} ${layoutMode === 3 ? styles.gridLayout : ''}`}>

            {/* ═══ 1. HEADER — only drag-enabled element ═══ */}
            <PanelHeader>
                <div className={styles.studioLabel}>
                    <Icon name="Building" color="#FFFFFF" size={24} />
                    <span className={styles.buildingName}>Area of Effect</span>
                </div>
                <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center' }}>
                    <div className={styles.efficiencyBar}>
                        <div className={styles.efficiencyFill} style={{ width: `${Math.min(100, Math.max(0, buildingEfficiency))}%` }}></div>
                    </div>
                    <div
                        onClick={() => trigger('area_of_effect', 'togglePanel')}
                        style={{ marginLeft: '12rem', padding: '0 8rem', cursor: 'pointer', color: '#A0AAB2', fontSize: '14rem', fontWeight: 'bold' }}
                    >
                        Close
                    </div>
                </div>
            </PanelHeader>

            {/* ═══ 2. SCROLL BODY — all interactive content inside ═══ */}
            <ScrollContainer className={styles.scrollBody} onMouseDown={(e: React.MouseEvent) => e.stopPropagation()}>

                {/* ─── Selected Building Stats ─────────────────────────── */}
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <div className={styles.cardHeader}>
                            <span className={styles.sectionTitle}>Selected Building</span>
                        </div>
                        {(!buildingName || buildingName === 'No Building Selected') ? (
                            <div className={styles.layerRowNative} style={{ paddingBottom: '8rem' }}>
                                <span className={styles.statRowLabel} style={{ width: '100%', fontStyle: 'italic', color: '#888' }}>Select a building to view its effects</span>
                            </div>
                        ) : (
                            <>
                                <div className={styles.layerRowNative} style={{ marginBottom: '8rem', borderBottom: '1px solid rgba(255,255,255,0.05)', paddingBottom: '8rem' }}>
                                    <span className={styles.statRowValue} style={{ fontSize: '16rem', color: '#FFF' }}>{buildingName}</span>
                                </div>
                                <div className={styles.layerRowNative}>
                                    <span className={styles.statRowLabel}>Well-being</span>
                                    <span className={styles.statRowValue}>{buildingWellbeing > 0 ? '+' : ''}{Math.round(buildingWellbeing)}%</span>
                                </div>
                                <div className={styles.layerRowNative}>
                                    <span className={styles.statRowLabel}>Service Reach</span>
                                    <span className={styles.statRowValue}>{Math.round(buildingServiceReach)}m</span>
                                </div>
                            </>
                        )}
                    </div>
                </div>

                {/* ─── UI Layout Sub-Card ──────────────────────────────── */}
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <div className={styles.cardHeader}>
                            <span className={styles.sectionTitle}>UI Layout</span>
                        </div>

                        {/* Layout Mode: Card(0), Sidebar(1), Floating(2), Grid(3) */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '12rem' }}>
                            <span className={styles.statRowLabel} style={{ flexGrow: 1 }}>UI Layout Mode</span>
                            <div style={{ display: 'flex', gap: '5rem' }}>
                                <div className={`${styles.footerBtn} ${layoutMode === 0 ? styles.footerBtnPrimary : ''}`} onClick={() => setLayoutMode(0)}>Card</div>
                                <div className={`${styles.footerBtn} ${layoutMode === 1 ? styles.footerBtnPrimary : ''}`} onClick={() => setLayoutMode(1)}>Sidebar</div>
                                <div className={`${styles.footerBtn} ${layoutMode === 2 ? styles.footerBtnPrimary : ''}`} onClick={() => setLayoutMode(2)}>Floating</div>
                                <div className={`${styles.footerBtn} ${layoutMode === 3 ? styles.footerBtnPrimary : ''}`} onClick={() => setLayoutMode(3)}>Grid</div>
                            </div>
                        </div>

                        {/* Visual Presets: Rings(0), Soft Glow(1), Classic(2) */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '12rem' }}>
                            <span className={styles.statRowLabel} style={{ flexGrow: 1 }}>Visual Preset</span>
                            <div style={{ display: 'flex', gap: '5rem' }}>
                                <div className={styles.footerBtn} onClick={() => setPreset(0)}>Rings</div>
                                <div className={styles.footerBtn} onClick={() => setPreset(1)}>Soft Glow</div>
                                <div className={styles.footerBtn} onClick={() => setPreset(2)}>Classic</div>
                            </div>
                        </div>

                        {/* Toggle: Show Stat Bubbles */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '12rem' }}>
                            <span className={styles.statRowLabel} style={{ flexGrow: 1 }}>Display Stat Bubbles</span>
                            <InputGuard>
                                <Toggle checked={showStats} onChange={toggleShowStats} />
                            </InputGuard>
                        </div>

                        {/* Toggle: High Visibility */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '12rem' }}>
                            <span className={styles.statRowLabel} style={{ flexGrow: 1 }}>High Visibility Mode</span>
                            <InputGuard>
                                <Toggle checked={highVis} onChange={toggleHighVis} />
                            </InputGuard>
                        </div>

                        {/* Slider: Max Label Distance */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '8rem' }}>
                            <span className={styles.statRowLabel}>Max Label Distance</span>
                            <div className={styles.sliderContainerNative}>
                                <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                    <Slider value={maxDistance} start={50} end={5000} onChange={(val: number) => setMaxDistance(val)} />
                                </InputGuard>
                                <span className={styles.sliderValText}>{Math.round(maxDistance)}m</span>
                            </div>
                        </div>
                    </div>
                </div>

                {/* ─── Visual Config Sub-Card ──────────────────────────── */}
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <div className={styles.cardHeader}>
                            <span className={styles.sectionTitle}>Visual Config</span>
                        </div>

                        {/* Global Opacity → setOpacity */}
                        <div className={styles.layerRowNative}>
                            <span className={styles.statRowLabel}>Global Opacity</span>
                            <div className={styles.sliderContainerNative}>
                                <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                    <Slider value={globalOpacity} start={0} end={100} onChange={(val: number) => setOpacity(val)} />
                                </InputGuard>
                                <span className={styles.sliderValText}>{Math.round(globalOpacity)}%</span>
                            </div>
                        </div>

                        {/* Global Radius → setSize */}
                        <div className={styles.layerRowNative}>
                            <span className={styles.statRowLabel}>Global Radius</span>
                            <div className={styles.sliderContainerNative}>
                                <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                    <Slider value={globalSize} start={50} end={2500} onChange={(val: number) => setSize(val)} />
                                </InputGuard>
                                <span className={styles.sliderValText}>{Math.round(globalSize)}m</span>
                            </div>
                        </div>

                        {/* Global Height → setHeight */}
                        <div className={styles.layerRowNative} style={{ marginBottom: '8rem' }}>
                            <span className={styles.statRowLabel}>Global Height</span>
                            <div className={styles.sliderContainerNative}>
                                <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                    <Slider value={globalHeight} start={0} end={200} onChange={(val: number) => setHeight(val)} />
                                </InputGuard>
                                <span className={styles.sliderValText}>{Math.round(globalHeight)}m</span>
                            </div>
                        </div>
                    </div>
                </div>

                {/* ─── Global Settings Layers ──────────────────────────── */}
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <div className={styles.cardHeader}>
                            <span className={styles.sectionTitle}>Global Settings</span>
                        </div>
                        {globalSettings.map((layer) => {
                            const layerColor = `rgba(${Math.round(layer.r * 255)}, ${Math.round(layer.g * 255)}, ${Math.round(layer.b * 255)}, 1)`;
                            return (
                                <div key={layer.id} style={{ marginBottom: '16rem', paddingBottom: '8rem' }}>
                                    <div className={styles.layerRowNative}>
                                        <InputGuard>
                                            <Toggle checked={layer.enabled} onChange={() => toggleGlobalLayer(layer.id, layer.enabled)} />
                                        </InputGuard>
                                        <div className={styles.layerIconWrapNative}>
                                            <Icon name={layer.id} color={layerColor} size={22} />
                                        </div>
                                        <div className={styles.bracketLabelNative} style={{ color: layerColor }}>{layer.name}</div>
                                    </div>
                                    <div className={styles.layerRowNative}>
                                        <span className={styles.statRowLabel}>Row Alpha</span>
                                        <div className={styles.sliderContainerNative}>
                                            <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                                <Slider value={(layer.opacity || 0) * 100} start={0} end={100} onChange={(val: number) => setGlobalLayerAlpha(layer.id, val)} />
                                            </InputGuard>
                                            <span className={styles.sliderValText}>{Math.round((layer.opacity || 0) * 100)}%</span>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>

                {/* ─── Local Effects Layers ─────────────────────────────── */}
                <div className={styles.section}>
                    <div className={styles.infoCard}>
                        <div className={styles.cardHeader}>
                            <span className={styles.sectionTitle}>Local Effects</span>
                        </div>
                        {localSettings.map((layer) => {
                            const layerColor = `rgba(${Math.round(layer.r * 255)}, ${Math.round(layer.g * 255)}, ${Math.round(layer.b * 255)}, 1)`;
                            return (
                                <div key={layer.id} style={{ marginBottom: '16rem', paddingBottom: '8rem' }}>
                                    <div className={styles.layerRowNative}>
                                        <InputGuard>
                                            <Toggle checked={layer.enabled} onChange={() => toggleLocalEffect(layer.id, layer.enabled)} />
                                        </InputGuard>
                                        <div className={styles.layerIconWrapNative}>
                                            <Icon name={layer.id} color={layerColor} size={22} />
                                        </div>
                                        <div className={styles.bracketLabelNative} style={{ color: layerColor }}>{layer.name}</div>
                                    </div>
                                    <div className={styles.layerRowNative}>
                                        <span className={styles.statRowLabel}>Row Alpha</span>
                                        <div className={styles.sliderContainerNative}>
                                            <InputGuard style={{ height: '14rem', flexGrow: 1 }}>
                                                <Slider value={(layer.opacity || 0) * 100} start={0} end={100} onChange={(val: number) => setLocalEffectAlpha(layer.id, val)} />
                                            </InputGuard>
                                            <span className={styles.sliderValText}>{Math.round((layer.opacity || 0) * 100)}%</span>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>

            </ScrollContainer>

            {/* ═══ 3. FOOTER ═══ */}
            <div className={styles.footerActions}>
                <div className={styles.footerBtn}>Collapse All</div>
                <div className={`${styles.footerBtn} ${styles.footerBtnPrimary}`}>Save Preset</div>
            </div>

        </Panel>
    );

    return (
        <div className={styles.fullScreenWrapper}>
            <DraggableWindow initialX={100} initialY={100} id="aoe_panel">
                {panelContent}
            </DraggableWindow>
        </div>
    );
};
