import React, { useState, useRef, useEffect } from 'react';
import { bindValue, trigger, useValue } from 'cs2/api';
import { Button } from 'cs2/ui';
import { AreaOfEffectPanel } from './AreaOfEffectPanel';
import { ErrorBoundary } from './ErrorBoundary';
import styles from './AreaOfEffect.module.scss';

const isPanelOpen$ = bindValue<boolean>('area_of_effect', 'isPanelOpen', false);

const useDraggable = (initX: number, initY: number) => {
    const [pos, setPos] = useState({ x: initX, y: initY });
    const drag = useRef({ active: false, startX: 0, startY: 0, ox: initX, oy: initY });

    useEffect(() => {
        const onMove = (e: MouseEvent) => {
            if (!drag.current.active) return;
            setPos({
                x: drag.current.ox + (e.clientX - drag.current.startX),
                y: drag.current.oy + (e.clientY - drag.current.startY),
            });
        };
        const onUp = () => { drag.current.active = false; };
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
        return () => { document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp); };
    }, []);

    const onMouseDown = (e: React.MouseEvent) => {
        drag.current = { active: true, startX: e.clientX, startY: e.clientY, ox: pos.x, oy: pos.y };
        e.stopPropagation();
    };

    return { pos, onMouseDown };
};

export const AreaOfEffectButton = () => {
    const isOpen = useValue(isPanelOpen$);
    const { pos, onMouseDown } = useDraggable(340, 10);

    const onClose = () => trigger('area_of_effect', 'togglePanel');

    return (
        <>
            <div className={styles.navButton}>
                <Button
                    variant="floating"
                    onSelect={() => trigger('area_of_effect', 'togglePanel')}
                    selected={isOpen}
                >
                    AoE
                </Button>
            </div>

            {isOpen && (
                <ErrorBoundary name="Main Panel">
                    <AreaOfEffectPanel />
                </ErrorBoundary>
            )}
        </>
    );
};
