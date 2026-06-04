import React, { useMemo } from 'react';
import { bindValue, useValue, trigger } from 'cs2/api';
import styles from './AreaOfEffect.module.scss';
import { Icon } from './Icons';
import { DraggableWindow, DragContext } from './DraggableWindow';
import { enableMiniInspector$ } from './AreaOfEffectPanel';

export const inspectorX$ = bindValue<number>('area_of_effect', 'inspectorX', 100);
export const inspectorY$ = bindValue<number>('area_of_effect', 'inspectorY', 300);
export const inspectorName$ = bindValue<string>('area_of_effect', 'inspectorName', '');
export const inspectorEffectsJson$ = bindValue<string>('area_of_effect', 'inspectorEffectsJson', '[]');

export const MiniInspector: React.FC = () => {
    const enableMiniInspector = useValue(enableMiniInspector$);
    const inspectorName = useValue(inspectorName$);
    const inspectorEffectsJson = useValue(inspectorEffectsJson$);
    const inspectorX = useValue(inspectorX$);
    const inspectorY = useValue(inspectorY$);

    const effects = useMemo(() => {
        try {
            return JSON.parse(inspectorEffectsJson);
        } catch (e) {
            return [];
        }
    }, [inspectorEffectsJson]);

    const handleDragEnd = React.useCallback((pos: { x: number; y: number }) => {
        trigger('area_of_effect', 'saveInspectorPosition', pos.x, pos.y);
    }, []);

    const onDrag = React.useContext(DragContext);

    if (!enableMiniInspector || !inspectorName || inspectorName === 'No Building Selected') {
        return null;
    }

    return (
        <DraggableWindow
            initialX={inspectorX}
            initialY={inspectorY}
            id="aoe_mini_inspector"
            onDragEnd={handleDragEnd}
        >
            <MiniInspectorCard
                inspectorName={inspectorName}
                effects={effects}
            />
        </DraggableWindow>
    );
};

interface MiniInspectorCardProps {
    inspectorName: string;
    effects: any[];
}

const MiniInspectorCard: React.FC<MiniInspectorCardProps> = ({ inspectorName, effects }) => {
    const onDrag = React.useContext(DragContext);

    return (
        <div className={styles.miniInspectorCard}>
            <div className={styles.miniHeader} onMouseDown={onDrag ?? undefined}>
                <div className={styles.miniTitleContainer}>
                    <Icon name="Building" color="#00A2E8" size={18} />
                    <div className={styles.miniTitle}>{inspectorName}</div>
                </div>
            </div>
            <div className={styles.miniBody}>
                {effects.length === 0 ? (
                    <div className={styles.miniNoEffects}>No active range stats</div>
                ) : (
                    <div className={styles.miniEffectsList}>
                        {effects.map((eff, index) => {
                            const isPositive = eff.value && eff.value.startsWith('+');
                            const isNegative = eff.value && eff.value.startsWith('-');
                            const valueClass = isPositive
                                ? styles.miniValuePositive
                                : isNegative
                                ? styles.miniValueNegative
                                : styles.miniValueDefault;

                            return (
                                <div key={index} className={styles.miniRow}>
                                    <div className={styles.miniRowLeft}>
                                        <div className={styles.miniIconWrap}>
                                            <Icon name={eff.icon || 'Healthcare'} color={eff.color || '#ffffff'} size={18} />
                                        </div>
                                        <div className={styles.miniTextContainer}>
                                            <div className={styles.miniEffectName}>{eff.name}</div>
                                            <div className={styles.miniEffectGroup}>{eff.group}</div>
                                        </div>
                                    </div>
                                    <div className={styles.miniRowRight}>
                                        {eff.value && <div className={valueClass}>{eff.value}</div>}
                                        {eff.range && <div className={styles.miniRange}>{eff.range}</div>}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </div>
    );
};
