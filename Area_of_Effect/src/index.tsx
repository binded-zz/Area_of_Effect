import { ModRegistrar } from "cs2/modding";
import { AreaOfEffectButton } from "mods/AreaOfEffectButton";
import { FloatingStatsOverlay } from "mods/FloatingStatsOverlay";
import { VanillaComponentResolver } from "mods/VanillaComponents";
import { ErrorBoundary } from "mods/ErrorBoundary";

const register: ModRegistrar = (moduleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);
    
    // Append both to GameTopLeft so they stay behind the system menus
    moduleRegistry.append('GameTopLeft', AreaOfEffectButton);
    moduleRegistry.append('GameTopLeft', () => (
        <ErrorBoundary name="Floating Stats">
            <FloatingStatsOverlay />
        </ErrorBoundary>
    ));
}

export default register;