import { useState, useEffect, useRef } from 'react';

export const useDraggable = (initialPosition: { x: number, y: number }) => {
    const [position, setPosition] = useState(initialPosition);
    const isDragging = useRef(false);
    const dragStart = useRef({ x: 0, y: 0 });
    const positionStart = useRef(initialPosition);

    const onMouseDown = (e: React.MouseEvent) => {
        isDragging.current = true;
        dragStart.current = { x: e.clientX, y: e.clientY };
        positionStart.current = position;
    };

    useEffect(() => {
        const onMouseMove = (e: MouseEvent) => {
            if (!isDragging.current) return;
            const deltaX = e.clientX - dragStart.current.x;
            const deltaY = e.clientY - dragStart.current.y;
            setPosition({
                x: positionStart.current.x + deltaX,
                y: positionStart.current.y + deltaY
            });
        };

        const onMouseUp = () => {
            isDragging.current = false;
        };

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);

        return () => {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        };
    }, [position]);

    return { position, onMouseDown };
};
