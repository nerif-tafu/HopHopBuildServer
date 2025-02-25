const ServerControl = () => {
    const [status, setStatus] = React.useState('stopped');
    const [isLoading, setIsLoading] = React.useState(false);
    const [logs, setLogs] = React.useState([]);
    const [uptime, setUptime] = React.useState(null);
    const [isEnabled, setIsEnabled] = React.useState(false);
    const [toast, setToast] = React.useState(null);
    const logsRef = React.useRef(null);

    const formatUptime = (seconds) => {
        if (!seconds) return 'Unknown';
        const days = Math.floor(seconds / 86400);
        const hours = Math.floor((seconds % 86400) / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const parts = [];
        if (days > 0) parts.push(`${days}d`);
        if (hours > 0) parts.push(`${hours}h`);
        parts.push(`${minutes}m`);
        return parts.join(' ');
    };

    React.useEffect(() => {
        // Initial status fetch
        fetchStatus();
        
        // Set up WebSocket connection
        const socket = io({
            transports: ['websocket'],
            upgrade: false
        });

        socket.on('server_control_status', (data) => {
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                setStatus(data.status);
                setUptime(data.uptime);
                setIsEnabled(data.enabled);
                // Only set initial logs from status
                if (data.logs) {
                    setLogs(data.logs);
                }
            }
        });

        // Add listener for log updates
        socket.on('server_control_logs', (data) => {
            if (data.logs) {
                setLogs(prevLogs => [...prevLogs, data.logs]);
            }
        });

        return () => socket.disconnect();
    }, []);

    // Auto-scroll effect
    React.useEffect(() => {
        if (logsRef.current) {
            logsRef.current.scrollTop = logsRef.current.scrollHeight;
        }
    }, [logs]);

    const fetchStatus = async () => {
        try {
            const response = await fetch('/api/server/control', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ action: 'status' })
            });
            const data = await response.json();
            if (data.error) throw new Error(data.error);
            
            setStatus(data.message.status);
            setLogs(data.message.startup_logs || []);
            setUptime(data.message.uptime);
            setIsEnabled(data.message.enabled);
        } catch (error) {
            showToast(error.message, 'error');
        }
    };

    const handleControl = async (action) => {
        setIsLoading(true);
        try {
            const response = await fetch('/api/server/control', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ action })
            });
            const data = await response.json();
            
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                showToast(data.message, 'success');
                if (action === 'start') setStatus('starting');
                else if (action === 'stop') setStatus('stopped');
                else if (action === 'restart') setStatus('restarting');
            }
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const showToast = (message, type = 'info') => {
        setToast({ message, type });
    };

    return (
        <div className="h-full flex flex-col">
            <div className="flex-1 min-h-0">
                <div className="bg-surface-light rounded-lg p-2 sm:p-4 h-full flex flex-col">
                    {/* Control header */}
                    <div className="mb-2 sm:mb-4 p-2 sm:p-4 bg-surface rounded-lg border border-surface-lighter">
                        <div className="flex flex-col gap-4">
                            <div className="flex flex-wrap items-center gap-2">
                                <div className="flex items-center gap-2">
                                    <span className={`
                                        ${status === 'running' ? 'text-green-500' : ''}
                                        ${status === 'stopped' ? 'text-red-500' : ''}
                                        ${status === 'starting' ? 'text-yellow-500' : ''}
                                        ${status === 'error' ? 'text-red-500' : ''}
                                    `}>
                                        {status.charAt(0).toUpperCase() + status.slice(1)}
                                    </span>
                                    {uptime && status === 'running' && (
                                        <span className="text-sm text-neutral-400">
                                            Uptime: {formatUptime(uptime)}
                                        </span>
                                    )}
                                </div>
                            </div>
                            <div className="flex flex-wrap gap-2">
                                <button
                                    onClick={() => handleControl('start')}
                                    disabled={isLoading || status === 'running' || status === 'starting'}
                                    className="px-4 py-2 rounded bg-green-500 hover:bg-green-600 
                                        text-white disabled:opacity-50 transition-colors"
                                >
                                    Start
                                </button>
                                <button
                                    onClick={() => handleControl('stop')}
                                    disabled={isLoading || status === 'stopped'}
                                    className="px-4 py-2 rounded bg-red-500 hover:bg-red-600 
                                        text-white disabled:opacity-50 transition-colors"
                                >
                                    Stop
                                </button>
                                <button
                                    onClick={() => handleControl('restart')}
                                    disabled={isLoading || status === 'stopped'}
                                    className="px-4 py-2 rounded bg-yellow-500 hover:bg-yellow-600 
                                        text-white disabled:opacity-50 transition-colors"
                                >
                                    Restart
                                </button>
                                <button
                                    onClick={() => handleControl(isEnabled ? 'disable' : 'enable')}
                                    disabled={isLoading}
                                    className={`px-4 py-2 rounded text-white disabled:opacity-50 ${
                                        isEnabled ? 'bg-neutral-500' : 'bg-blue-500'
                                    }`}
                                    title={isEnabled ? 'Disable auto-start on boot' : 'Enable auto-start on boot'}
                                >
                                    {isEnabled ? 'Disable' : 'Enable'}
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* Logs section */}
                    <div className="flex-1 min-h-0 bg-surface border border-surface-lighter">
                        <div ref={logsRef}
                            className="h-full p-2 sm:p-4 overflow-y-auto font-mono text-xs sm:text-sm scrollbar-thin 
                                scrollbar-thumb-neutral-600 scrollbar-track-neutral-800 
                                hover:scrollbar-thumb-neutral-500 bg-neutral-800"
                        >
                            <pre className="text-white whitespace-pre-wrap break-words overflow-x-hidden w-full">
                                {logs.join('\n')}
                            </pre>
                        </div>
                    </div>
                </div>
            </div>
            {toast && (
                <Toast
                    message={toast.message}
                    type={toast.type}
                    onClose={() => setToast(null)}
                />
            )}
        </div>
    );
};

window.ServerControl = ServerControl; 