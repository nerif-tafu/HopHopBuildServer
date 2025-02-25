const App = () => {
    const [serverStatus, setServerStatus] = React.useState({
        status: 'unknown',
        players: 'Unknown',
        max_players: 'Unknown',
        fps: 'Unknown',
        entities: 'Unknown',
        raw: '',
        console: ''
    });
    const [connectionState, setConnectionState] = React.useState('disconnected');
    const [currentPage, setCurrentPage] = React.useState('status');

    React.useEffect(() => {
        const socket = io({
            transports: ['websocket'],
            upgrade: false
        });

        socket.on('connect', () => {
            setConnectionState('connected');
        });

        socket.on('disconnect', () => {
            setConnectionState('disconnected');
            setServerStatus(prev => ({ ...prev, status: 'offline' }));
        });

        socket.on('server_status', (msg) => {
            if (msg && msg.data) {
                setServerStatus(prev => ({
                    ...msg.data,
                    console: prev.console // Preserve console output
                }));
                setConnectionState(msg.data.status);
            }
        });

        socket.on('screen_output', (msg) => {
            if (msg && msg.data) {
                setServerStatus(prev => ({
                    ...prev,
                    console: prev.console ? `${prev.console}\n${msg.data}` : msg.data
                }));
            }
        });

        socket.emit('request_status');

        return () => socket.disconnect();
    }, []);

    const getStatusMessage = () => {
        switch (connectionState) {
            case 'connected': return 'Connected to WebSocket';
            case 'disconnected': return 'Disconnected from WebSocket';
            case 'online': return 'Console feed is LIVE';
            case 'offline': return 'Server Offline';
            default: return 'Unknown Status';
        }
    };

    const renderPage = () => {
        switch (currentPage) {
            case 'status':
                return (
                    <div className="h-full flex flex-col">
                        <div className="mb-6">
                            <h2 className="text-xl text-primary">Server Status</h2>
                        </div>
                        <div className="flex-1 min-h-0">
                            <div className="bg-surface-light rounded-lg p-4 h-full flex flex-col">
                                <ServerStatus status={serverStatus} />
                            </div>
                        </div>
                    </div>
                );
            case 'config':
                return <window.ConfigPage />;
            case 'control':
                return <window.ControlPage />;
            case 'plugins':
                return <window.PluginsPage />;
            default:
                return <div>Page not found</div>;
        }
    };

    return (
        <Layout currentPage={currentPage} onPageChange={setCurrentPage}>
            {renderPage()}
        </Layout>
    );
}; 