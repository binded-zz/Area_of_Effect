# Area of Effect (Cities: Skylines II Mod)

A premium, fully customizable real-time visualization tool that displays the **Area of Effect (AoE)**, ranges, capacities, and active service modifiers for all service and upgrade buildings in Cities: Skylines II.

---

## Features

### 1. Visualization Mode System

When you select a building, a **Visualization Mode** button strip appears in the Layers tab. Choose from five modes:

| Mode | Description |
|---|---|
| **Off** | Disables the visualization entirely |
| **Circle** | Draws the native 3D service radius disc on the ground — clean and fast |
| **Roads** | Highlights only the road segments that fall within the service range |
| **Polygon** | Runs a true Breadth-First Search across the road network and draws the real jagged isochrone boundary — the actual area reachable by road, not just a circle |
| **Heatmap** | Reads the engine's cached service coverage data and renders a heat gradient at road nodes |

> **How the Heatmap works:** Heatmap mode does not paint a continuous surface. It renders **at road nodes** — the junctions and connection points in the road network. Each node glows at an intensity proportional to how much coverage it receives, and **nodes fade off with distance** from the source building. The result is a constellation of glowing dots that dims the further out you go — an accurate picture of how service coverage decays in the simulation.

> **Tip:** For the cleanest view of any visualization mode, turn off **Local Effects** in the Layers tab. Local effect rings can overlap and obscure the overlay, especially in dense city centers.

---

### 2. Local & Global Layers

The mod can show a lot of information at once. Between local effect rings on every building and global service layer overlays, things can get busy fast.

- To inspect one building — **disable Global Layers** and use a Visualization Mode
- For a city-wide overview — **disable Local Effects** to reduce clutter
- Both can be toggled independently in the Layers tab
- Settings are **saved separately** for local and global layers

---

### 3. Dynamic In-Game UI Panel
- **Selected Building Details**: Shows precise service coverage values, active local modifiers, ranges, and city-wide modifiers.
- **Layers Customization**: Toggle individual global and local layers on/off, adjust their specific opacity, or customize their color directly in-game.
- **Presets Support**: Save up to 5 custom configurations (layer selections, colors, opacities, preset modes) and easily reload or overwrite them.

---

### 4. Custom Visual Overlays & Presets

Choose between three visual styles:
- **Neon Rings** — Concentric, sharp neon lines outlining boundary ranges
- **Soft Glow** — Concentric overlapping circles fading out to give a glowing heat map effect
- **Classic** — A single solid filled circle outlining the absolute outer limit

Adjust **Global Opacity**, **Global Radius**, and **Overlay Height** to prevent clipping with terrain and buildings.

---

### 5. Real-Time Floating Stat Bubbles

Displays compact floating icons and stat values directly above active buildings. Shows stats for:

- **Healthcare** (Ambulances) & **Deathcare** (Hearses)
- **Education** (Elementary, High School, College, University)
- **Well-being** & **Meals** modifiers
- **Police Patrol Cars** & **Crime Rate Modifiers**
- **Fire Engines**, **Fire Hazards**, & **Fire Response** speeds
- **Attractiveness** (Parks & Leisure)
- **Telecom Range** & **Post Vans**

Improvements in this update:
- Proper unit formatting — meters, student counts, vehicle counts all labelled correctly
- Duplicate filtering — the same stat won't appear twice on overlapping buildings
- Distance scaling — bubbles scale smaller as you zoom out
- **High Visibility Mode** — boosts contrast and font size for readability at distance
- **Bubble size slider** — scale the bubbles up or down to your preference

---

### 6. Mini Inspector

A small **draggable floating card** that appears when you select a building. Shows:
- The building name
- All active service effects in range (well-being, healthcare, education, etc.)
- Effect values with **color-coded positive (green) / negative (red)** indicators
- The range of each effect

Enable or disable it in the Settings tab. Its position is saved between sessions and it disappears automatically when no building is selected.

---

### 7. Pre-Placement Range Ring

When placing a new building the mod draws the **range boundary ring on the terrain before you place it**, so you can see exactly where coverage will reach before committing. Can be toggled off in settings.

---

## Settings Reference

| Setting | Description |
|---|---|
| **Enable Mod** | Master on/off switch |
| **Enable Mini Inspector** | Show or hide the floating building card |
| **Enable Pre-placement Ring** | Range ring preview while placing buildings |
| **Show Overlay on Hover** | Show the AoE radius when hovering a building without clicking |
| **High Visibility Mode** | Boost stat bubble contrast and font size |
| **Floating Stats Scale** | Resize the stat bubbles (50–200%) |
| **Label Visibility Distance** | How far out labels remain visible when zoomed out (up to 3,600 m) |
| **Overlay Opacity** | Transparency of the AoE overlay |
| **Overlay Height** | Nudge overlay height above ground to avoid z-fighting |
| **Global Circle Size** | Scale of global service indicator circles |
| **Visual Preset** | Rings, Glow, or Classic rendering style |
| **Saved Presets** | Up to 5 custom layer configurations, saved and restored |

---

## Installation

1. Subscribe to the mod on PDX Mods.
2. In-game, open the main panel using the **AoE button** on your toolbar.
3. Select any building to see its coverage overlays and stats.
4. Use the **Layers tab** to switch visualization modes and toggle local/global layers.
5. Use the **Settings tab** to configure the display to your preference.

---

## Performance

The visualization logic runs in a dedicated `VisualizationDispatcherSystem` fully decoupled from the UI. All heavy geometry work — BFS pathfinding, polygon decimation, heatmap sampling — runs in **Burst-compiled parallel jobs** and does not block the main thread. Virtually zero CPU overhead and no UI thread hitching.

---

## Links

- [PDX Mods Page](https://mods.paradoxplaza.com/mods/145021/Windows)
- [Forum Thread](https://forum.paradoxplaza.com/forum/threads/testing-area-of-effect-native-aura-radius-visualizer.1924429/)
- [GitHub](https://github.com/binded-zz/Area_of_Effect)
