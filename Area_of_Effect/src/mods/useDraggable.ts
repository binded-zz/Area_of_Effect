import { useState, useCallback, useRef, useEffect } from 'react';

export const useDraggable = (id: string, initialPosition: { x: number, y: number }) => {
    // Load from localStorage or use initial
    const [position, setPosition] = useState(() => {
        try {
            const saved = localStorage.getItem(`aoe_window_${id}`);
            if (saved) return JSON.parse(saved);
        } catch {}
        return initialPosition;
    });

    const dragState = useRef<{ startX: number; startY: number; posX: number; posY: number } | null>(null);

    const onMouseDown = useCallback((e: React.MouseEvent) => {
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

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }, [position]);

    // Save to localStorage on change
    useEffect(() => {
        try {
            localStorage.setItem(`aoe_window_${id}`, JSON.stringify(position));
        } catch {}
    }, [position, id]);

    return { position, onMouseDown };
};
