# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-06-03

### Added
- **Presets Support**: Added custom configuration presets saving/loading slot system (up to 5 presets slots available in-game).
- **Draggable Windows Position Persistence**: Draggable positions for the main panel and Mini-Inspector are saved and persistent across game launches.
- **High Visibility Mode**: Toggle option in settings to scale up stat bubbles and boost contrast for easier reading.

### Changed
- **Unit and Value Spacing**: Enforced uniform formatting with a space between numeric values and units (`%`, `m`) throughout the entire mod (layers opacity sliders, settings sliders, selected building details, and floating stat bubbles).
- **Settings Slider Widths**: Widened all settings tab sliders (track expanded to `215rem` and container to `292rem`) to maximize layout usage, resolve empty space gaps, and enable more precise value adjustments.
- **Max Label Distance Limit**: Standardized the maximum label visibility distance to `3,600 m` across UI sliders, C# bindings, and Options configuration parameters.
- **Font Sizing & Scaling**: Reduced text sizing across panel components to eliminate clipping under higher UI scaling.
- **Mini-Inspector Sizing**: Expanded the layout width of the inspector pop-up card to `360rem` and scaled up labels, colors, and icons for clean legibility.

### Fixed
- **Wellbeing/Health Modifier Display**: Separated absolute local modifiers (like Wellbeing and Health) from relative percentage modifiers. Wellbeing modifier values now display as absolute values (e.g. `+1`) rather than multiplying by 100 and appending a percentage symbol (e.g., `+100 %`).
- **Dark Text Contrast Issue**: Swapped `span` elements with `div` elements and enforced styling priorities to prevent Cities: Skylines II's global vanilla stylesheets from overriding panel text to black.
- **Cohtml Rendering Crash Prevention**: Maintained a static layout tree structure for inline SVG elements in overlays to prevent native Cohtml layout crashes.
