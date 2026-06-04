import { useState, useCallback, useRef, useEffect } from 'react';

export const useDraggable = (
    id: string,
    initialPosition: { x: number; y: number },
    onDragEnd?: (pos: { x: number; y: number }) => void
) => {
    const [position, setPosition] = useState(initialPosition);
    
    const positionRef = useRef(position);
    positionRef.current = position;

    const dragState = useRef<{ startX: number; startY: number; posX: number; posY: number } | null>(null);

    // Sync with initialPosition updates from the C# backend (e.g. on loading settings or factory resets)
    useEffect(() => {
        if (!dragState.current) {
            setPosition(initialPosition);
        }
    }, [initialPosition.x, initialPosition.y]);

    const onMouseDown = useCallback((e: React.MouseEvent) => {
        if (e.button !== 0) return;

        const startX = e.clientX;
        const startY = e.clientY;
        const posX = positionRef.current.x;
        const posY = positionRef.current.y;

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
            if (dragState.current) {
                onDragEnd?.(positionRef.current);
            }
            dragState.current = null;
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        };

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }, [onDragEnd]);

    return { position, onMouseDown };
};
