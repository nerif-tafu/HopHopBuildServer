const ServerControl = () => {
    const [status, setStatus] = React.useState('stopped');
    const [isLoading, setIsLoading] = React.useState(false);
    const [logs, setLogs] = React.useState([]);
    const [uptime, setUptime] = React.useState(null);
    const [isEnabled, setIsEnabled] = React.useState(false);
    const [toast, setToast] = React.useState(null);
    const logsRef = React.useRef(null);
    const statusInterval = React.useRef(null);

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
        
        // Set up periodic status updates
        statusInterval.current = setInterval(fetchStatus, 5000);
        
        return () => {
            if (statusInterval.current) {
                clearInterval(statusInterval.current);
            }
        };
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
        <div className="bg-surface p-4 rounded-lg flex flex-col h-full">
            <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-4">
                    <div className="flex items-center gap-2">
                        <h2 className="text-lg font-semibold">Server Control</h2>
                        <span className={`
                            ${status === 'running' ? 'text-green-600' : ''}
                            ${status === 'stopped' ? 'text-red-600' : ''}
                            ${status === 'starting' ? 'text-yellow-600' : ''}
                            ${status === 'error' ? 'text-red-600' : ''}
                        `}>
                            {status.charAt(0).toUpperCase() + status.slice(1)}
                        </span>
                    </div>
                    {uptime && status === 'running' && (
                        <span className="text-sm text-neutral-500">
                            Uptime: {formatUptime(uptime)}
                        </span>
                    )}
                </div>
                <div className="flex gap-2">
                    <button
                        onClick={() => handleControl('start')}
                        disabled={isLoading || status === 'running' || status === 'starting'}
                        className="px-4 py-2 rounded bg-green-600 text-white disabled:opacity-50"
                    >
                        Start
                    </button>
                    <button
                        onClick={() => handleControl('stop')}
                        disabled={isLoading || status === 'stopped'}
                        className="px-4 py-2 rounded bg-red-600 text-white disabled:opacity-50"
                    >
                        Stop
                    </button>
                    <button
                        onClick={() => handleControl('restart')}
                        disabled={isLoading || status === 'stopped'}
                        className="px-4 py-2 rounded bg-yellow-600 text-white disabled:opacity-50"
                    >
                        Restart
                    </button>
                    <button
                        onClick={() => handleControl(isEnabled ? 'disable' : 'enable')}
                        disabled={isLoading}
                        className={`px-4 py-2 rounded text-white disabled:opacity-50 ${
                            isEnabled ? 'bg-neutral-500' : 'bg-blue-600'
                        }`}
                        title={isEnabled ? 'Disable auto-start on boot' : 'Enable auto-start on boot'}
                    >
                        {isEnabled ? 'Disable' : 'Enable'}
                    </button>
                </div>
            </div>

            {/* Service logs */}
            <div className="flex-1 min-h-0 bg-neutral-800 rounded">
                <div 
                    ref={logsRef}
                    className="h-full p-2 overflow-y-auto font-mono text-sm"
                >
                    <pre className="text-white whitespace-pre-wrap break-words overflow-x-hidden w-full">
                        {logs.join('\n')}
                    </pre>
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