import { ModRegistrar } from "cs2/modding";
import { AreaOfEffectButton } from "mods/AreaOfEffectButton";
import { FloatingStatsOverlay } from "mods/FloatingStatsOverlay";
import { VanillaComponentResolver } from "mods/VanillaComponents";
import { ErrorBoundary } from "mods/ErrorBoundary";

const register: ModRegistrar = (moduleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);
    
    // Append the button to TopLeft, but the overlay to Game so it renders behind main menus
    moduleRegistry.append('GameTopLeft', AreaOfEffectButton);
    moduleRegistry.append('Game', () => (
        <ErrorBoundary name="Floating Stats">
            <FloatingStatsOverlay />
        </ErrorBoundary>
    ));
}

export default register;