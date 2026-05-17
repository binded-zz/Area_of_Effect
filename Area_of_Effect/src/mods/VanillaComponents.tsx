import { Color } from 'cs2/bindings';
import { ModuleRegistry } from 'cs2/modding';
import { ReactNode, useState, useEffect, useRef } from 'react';
import styles from './AreaOfEffect.module.scss';

export interface PropsColorField {
    focusKey?: any;
    disabled?: boolean;
    value?: Color;
    className?: string;
    alpha?: boolean;
    onChange?: (e: Color) => void;
}

export interface SliderProps {
    value: number;
    start: number;
    end: number;
    disabled?: boolean;
    onChange?: (value: number) => void;
    onDragStart?: () => void;
    onDragEnd?: () => void;
}

export interface ToggleProps {
    checked: boolean;
    disabled?: boolean;
    onChange?: (value: boolean) => void;
}

const registryIndex = {
    ColorField: ['game-ui/common/input/color-picker/color-field/color-field.tsx', 'ColorField'],
    Slider: ['game-ui/common/input/slider/slider.tsx', 'Slider'],
    Toggle: ['game-ui/common/input/toggle/toggle.tsx', 'Toggle'],
    Scrollable: ['game-ui/common/scrollable/scrollable.tsx', 'Scrollable'],
    Panel: ['game-ui/common/panel/panel.tsx', 'Panel'],
    PanelSection: ['game-ui/common/panel/panel-section.tsx', 'PanelSection'],
    PanelSectionRow: ['game-ui/common/panel/panel-section-row.tsx', 'PanelSectionRow'],
};

export class VanillaComponentResolver {
    public static get instance(): VanillaComponentResolver | undefined {
        return this._instance;
    }
    private static _instance?: VanillaComponentResolver;

    public static setRegistry(in_registry: ModuleRegistry) {
        this._instance = new VanillaComponentResolver(in_registry);
    }
    private registryData: ModuleRegistry;

    constructor(in_registry: ModuleRegistry) {
        this.registryData = in_registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {};
    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        try {
            const module = this.registryData.registry.get(entryData[0]);
            if (!module) {
                console.warn(`[AoE] Module missing: ${entryData[0]}`);
                return (this.cachedData[entry] = (props: any) => <div className={`missing-module-${entry}`}>{props.children}</div>);
            }
            const comp = module[entryData[1]];
            if (!comp) {
                console.warn(`[AoE] Component ${entryData[1]} missing in ${entryData[0]}`);
                return (this.cachedData[entry] = (props: any) => <div className={`missing-comp-${entry}`}>{props.children}</div>);
            }
            return (this.cachedData[entry] = comp);
        } catch (e) {
            console.error(`[AoE] Resolution error for ${entry}:`, e);
            return (this.cachedData[entry] = (props: any) => <div className={`resolution-error-${entry}`}>{props.children}</div>);
        }
    }

    public get ColorField(): (props: PropsColorField) => JSX.Element {
        return this.cachedData['ColorField'] ?? this.updateCache('ColorField');
    }
    public get Slider(): (props: SliderProps) => JSX.Element {
        return this.cachedData['Slider'] ?? this.updateCache('Slider');
    }
    public get Toggle(): (props: ToggleProps) => JSX.Element {
        return this.cachedData['Toggle'] ?? this.updateCache('Toggle');
    }
    public get Scrollable(): (props: any) => JSX.Element {
        return this.cachedData['Scrollable'] ?? this.updateCache('Scrollable');
    }
    public get Panel(): (props: any) => JSX.Element {
        return this.cachedData['Panel'] ?? this.updateCache('Panel');
    }
    public get PanelSection(): (props: any) => JSX.Element {
        return this.cachedData['PanelSection'] ?? this.updateCache('PanelSection');
    }
    public get PanelSectionRow(): (props: any) => JSX.Element {
        return this.cachedData['PanelSectionRow'] ?? this.updateCache('PanelSectionRow');
    }
}
