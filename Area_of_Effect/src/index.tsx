import { ModRegistrar } from "cs2/modding";
import { AreaOfEffectButton } from "mods/AreaOfEffectButton";
import { FloatingStatsOverlay } from "mods/FloatingStatsOverlay";
import { VanillaComponentResolver } from "mods/VanillaComponents";
import { ErrorBoundary } from "mods/ErrorBoundary";
import { VisualizationOverlay } from "mods/VisualizationOverlay";
import { MiniInspector } from "mods/MiniInspector";

const register: ModRegistrar = (moduleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // ?? Existing overlays ?????????????????????????????????????????????????
    moduleRegistry.append('GameTopLeft', AreaOfEffectButton);
    moduleRegistry.append('Game', () => (
        <ErrorBoundary name="Floating Stats">
            <FloatingStatsOverlay />
        </ErrorBoundary>
    ));

    // ?? Visualization overlay (SVG, direct DOM mutations) ?????????????????
    moduleRegistry.append('Game', () => (
        <ErrorBoundary name="Visualization Overlay">
            <VisualizationOverlay />
        </ErrorBoundary>
    ));

    // ?? Mini-Inspector ????????????????????????????????????????????????????
    moduleRegistry.append('Game', () => (
        <ErrorBoundary name="Mini Inspector">
            <MiniInspector />
        </ErrorBoundary>
    ));
};

// Force re-emit
export default register;