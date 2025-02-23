const App = () => {
    const [serverStatus, setServerStatus] = React.useState({
        status: 'unknown',
        players: 'Unknown',
        max_players: 'Unknown',
        fps: 'Unknown',
        entities: 'Unknown',
        raw: ''
    });
    const [connectionState, setConnectionState] = React.useState('disconnected');
    const [consoleOutput, setConsoleOutput] = React.useState('');
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
        });

        socket.on('server_status', (msg) => {
            if (msg && msg.data) {
                setServerStatus(msg.data);
                setConnectionState(msg.data.status); // 'online', 'offline', or 'unknown'
            }
        });

        socket.on('screen_output', (msg) => {
            if (msg && msg.data) {
                setConsoleOutput(msg.data);
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
                    <React.Fragment>
                        <ServerStatus status={serverStatus} />
                        <ConsoleOutput output={consoleOutput} />
                        <ConnectionStatus state={connectionState} message={getStatusMessage()} />
                    </React.Fragment>
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