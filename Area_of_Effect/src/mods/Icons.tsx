import React from 'react';

// Cities: Skylines 2 Standard SVG paths (hard-coded for absolute stability)
export const IconWellbeing = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" fill={color}/>
    </svg>
);

export const IconPolice = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm-1 11H7V10h4V7h2v3h4v2h-4v3h-2v-3z" fill={color}/>
    </svg>
);

export const IconFire = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M19.48,12.35c-1.57-4.08-7.16-4.3-5.81-10.23c0.1-0.44-0.37-0.78-0.75-0.55C9.29,3.71,6.68,8,8.87,13.62 c0.18,0.46-0.36,0.89-0.75,0.59c-1.81-1.37-2-3.34-1.84-4.75c0.06-0.52-0.62-0.77-0.91-0.34C4.69,10.16,4,11.84,4,14.37 c0,4.38,3.54,7.92,7.92,7.92c4.38,0,7.92-3.54,7.92-7.92C19.85,13.75,19.72,13,19.48,12.35z" fill={color}/>
    </svg>
);

export const IconParks = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M17,12h2L12,2L5.05,12H7l-3.95,6h6.95v4h4v-4h6.95L17,12z" fill={color}/>
    </svg>
);

export const IconHealthcare = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M19,3H5C3.89,3,3,3.9,3,5v14c0,1.1,0.89,2,2,2h14c1.1,0,2-0.9,2-2V5C21,3.9,20.11,3,19,3z M18,13h-5v5h-2v-5H6v-2h5V6h2v5h5V13z" fill={color}/>
    </svg>
);

export const IconTelecom = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 14h-2v-2h2v2zm0-4h-2V7h2v5z" fill={color}/>
    </svg>
);

export const IconPostal = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z" fill={color}/>
    </svg>
);

export const IconEducation = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M5 13.18v4L12 21l7-3.82v-4L12 17l-7-3.82zM12 3L1 9l11 6 9-4.91V17h2V9L12 3z" fill={color}/>
    </svg>
);

export const IconPollution = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M19.48,12.35c-1.57-4.08-7.16-4.3-5.81-10.23c0.1-0.44-0.37-0.78-0.75-0.55C9.29,3.71,6.68,8,8.87,13.62 c0.18,0.46-0.36,0.89-0.75,0.59c-1.81-1.37-2-3.34-1.84-4.75c0.06-0.52-0.62-0.77-0.91-0.34C4.69,10.16,4,11.84,4,14.37 c0,4.38,3.54,7.92,7.92,7.92c4.38,0,7.92-3.54,7.92-7.92C19.85,13.75,19.72,13,19.48,12.35z" fill={color}/>
    </svg>
);

export const IconEfficiency = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M13,3H6V21H18V15H13V3M11,19H8V17H11V19M11,15H8V13H11V15M11,11H8V9H11V11M11,7H8V5H11V7M16,19H13V17H16V19M16,15H13V13H16V15" fill={color}/>
    </svg>
);

export const IconMeals = ({ size = 20, color = 'white' }) => (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M11 9H9V2H7v7H5V2H3v7c0 2.12 1.66 3.84 3.75 3.97V22h2.5v-9.03C11.34 12.84 13 11.12 13 9V2h-2v7zm5-3v8h2.5v8H21V2c-2.76 0-5 2.24-5 4z" fill={color}/>
    </svg>
);

export const getIconForLayer = (id: string, size: number, color: string) => {
    if (!id || typeof id !== 'string') return <IconWellbeing size={size} color={color} />;
    const lid = id.toLowerCase();
    if (lid.includes('wellbeing') || lid.includes('well-being')) return <IconWellbeing size={size} color={color} />;
    if (lid.includes('health') || lid.includes('medical') || lid.includes('hospital')) return <IconHealthcare size={size} color={color} />;
    if (lid.includes('police')) return <IconPolice size={size} color={color} />;
    if (lid.includes('fire')) return <IconFire size={size} color={color} />;
    if (lid.includes('parks') || lid.includes('leisure')) return <IconParks size={size} color={color} />;
    if (lid.includes('elementary') || lid.includes('school') || lid.includes('highschool') || lid.includes('college') || lid.includes('university') || lid.includes('edu')) return <IconEducation size={size} color={color} />;
    if (lid.includes('telecom')) return <IconTelecom size={size} color={color} />;
    if (lid.includes('post') || lid.includes('postal')) return <IconPostal size={size} color={color} />;
    if (lid.includes('attract')) return <IconWellbeing size={size} color={color} />;
    if (lid.includes('pollution') || lid.includes('noise')) return <IconPollution size={size} color={color} />;
    if (lid.includes('efficiency')) return <IconEfficiency size={size} color={color} />;
    if (lid.includes('meals')) return <IconMeals size={size} color={color} />;
    return <IconWellbeing size={size} color={color} />; 
};

export const Icon: React.FC<{ name: string; className?: string; color?: string; size?: number }> = ({ name, className, color = 'white', size = 20 }) => {
    return (
        <div className={className}>
            {getIconForLayer(name, size, color)}
        </div>
    );
};
