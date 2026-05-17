import React, { Component, ErrorInfo, ReactNode } from 'react';

interface Props {
    children: ReactNode;
    name?: string;
}

interface State {
    hasError: boolean;
    error: Error | null;
    errorInfo: ErrorInfo | null;
}

export class ErrorBoundary extends Component<Props, State> {
    public state: State = {
        hasError: false,
        error: null,
        errorInfo: null
    };

    public static getDerivedStateFromError(error: Error): State {
        // Update state so the next render will show the fallback UI.
        return { hasError: true, error, errorInfo: null };
    }

    public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        this.setState({ error, errorInfo });
        console.error(`Uncaught error in ErrorBoundary (${this.props.name || 'Unknown'}):`, error, errorInfo);
    }

    private handleReset = () => {
        this.setState({ hasError: false, error: null, errorInfo: null });
    };

    public render() {
        if (this.state.hasError) {
            return (
                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    backgroundColor: '#1A262F',
                    border: '1px solid #FF5555',
                    borderRadius: '6px',
                    padding: '20px',
                    margin: '10px',
                    width: '100%',
                    height: '100%',
                    boxSizing: 'border-box',
                    overflowY: 'auto'
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
                        <h3 style={{ color: '#FFFFFF', margin: 0, fontSize: '16px', fontWeight: 'bold' }}>
                            [AoE Mod] Runtime Exception Caught
                        </h3>
                        <div 
                            onClick={this.handleReset}
                            style={{
                                backgroundColor: '#00A2E8',
                                color: '#FFFFFF',
                                padding: '6px 12px',
                                borderRadius: '4px',
                                cursor: 'pointer',
                                fontWeight: 'bold',
                                fontSize: '12px'
                            }}
                        >
                            Reset Component UI
                        </div>
                    </div>
                    
                    <div style={{ backgroundColor: '#0D1117', padding: '10px', borderRadius: '4px', overflowX: 'auto', marginBottom: '10px' }}>
                        <div style={{ color: '#FF7777', fontWeight: 'bold', marginBottom: '5px', fontSize: '13px' }}>
                            {this.props.name && <span>{this.props.name}: </span>}
                            {this.state.error?.message || 'Unknown Error'}
                        </div>
                        <pre style={{ margin: 0 }}>
                            <code style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontSize: '12rem', color: '#FF7777' }}>
                                {this.state.error?.stack || 'No stack trace available.'}
                            </code>
                        </pre>
                    </div>

                    {this.state.errorInfo && (
                        <div style={{ backgroundColor: '#0D1117', padding: '10px', borderRadius: '4px', overflowX: 'auto' }}>
                            <div style={{ color: '#A0AAB2', fontWeight: 'bold', marginBottom: '5px', fontSize: '13px' }}>
                                Component Stack:
                            </div>
                            <pre style={{ margin: 0 }}>
                                <code style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all', fontSize: '12rem', color: '#A0AAB2' }}>
                                    {this.state.errorInfo.componentStack}
                                </code>
                            </pre>
                        </div>
                    )}
                </div>
            );
        }

        return this.props.children;
    }
}
