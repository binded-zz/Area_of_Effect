import { useState, useCallback, useRef } from 'react';

export const useDraggable = (initialPosition: { x: number, y: number }) => {
    const [position, setPosition] = useState(initialPosition);
    const dragState = useRef<{ startX: number; startY: number; posX: number; posY: number } | null>(null);

    const onMouseDown = useCallback((e: React.MouseEvent) => {
        // Only start drag on left button
        if (e.button !== 0) return;

        const startX = e.clientX;
        const startY = e.clientY;
        const posX = position.x;
        const posY = position.y;

        dragState.current = { startX, startY, posX, posY };

        const onMouseMove = (ev: MouseEvent) => {
            if (!dragState.current) return;
            const dx = ev.clientX - dragState.current.startX;
            const dy = ev.clientY - dragState.current.startY;
            setPosition({
                x: dragState.current.posX + dx,
                y: dragState.current.posY + dy
            });
        };

        const onMouseUp = () => {
            dragState.current = null;
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        };

        // Only add listeners when actively dragging
        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }, [position]);

    return { position, onMouseDown };
};
