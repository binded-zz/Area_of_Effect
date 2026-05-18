import React from 'react';
import { useDraggable } from './useDraggable';
import styles from './AreaOfEffect.module.scss';

interface Props {
    children: React.ReactNode;
    initialX: number;
    initialY: number;
    id?: string;
}

/**
 * DragContext provides the onMouseDown handler ONLY to the PanelHeader.
 * The outer container div has NO drag handler — this prevents the drag
 * system from hijacking mouse events on sliders, toggles, and other
 * interactive elements inside the panel body.
 */
export const DragContext = React.createContext<React.MouseEventHandler | null>(null);

export const DraggableWindow: React.FC<Props> = ({ children, initialX, initialY, id = "default_window" }) => {
    const { position, onMouseDown } = useDraggable(id, { x: initialX, y: initialY });

    return (
        <div
            className={styles.draggableContainer}
            style={{ left: position.x, top: position.y }}
            /* NO onMouseDown here — drag is header-only via DragContext */
        >
            <DragContext.Provider value={onMouseDown}>
                {children}
            </DragContext.Provider>
        </div>
    );
};
