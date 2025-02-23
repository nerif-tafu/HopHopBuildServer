const App = () => {
    const [serverStatus, setServerStatus] = React.useState({
        status: 'unknown',
        players: 'Unknown',
        max_players: 'Unknown',
        fps: 'Unknown',
        entities: 'Unknown',
        raw: ''
    });
    const [consoleOutput, setConsoleOutput] = React.useState('');
    const [connectionStatus, setConnectionStatus] = React.useState('Connecting to server...');
    const [connected, setConnected] = React.useState(false);
    const [currentPage, setCurrentPage] = React.useState('status');

    React.useEffect(() => {
        const socket = io({
            transports: ['websocket'],
            upgrade: false
        });

        socket.on('connect', () => {
            setConnectionStatus('Connected to WS - Looking for server output');
            setConnected(true);
        });

        socket.on('disconnect', () => {
            setConnectionStatus('Disconnected from server');
            setConnected(false);
        });

        socket.on('server_status', (msg) => {
            if (msg && msg.data) {
                setServerStatus(msg.data);
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

    const renderPage = () => {
        switch (currentPage) {
            case 'status':
                return (
                    <React.Fragment>
                        <ServerStatus status={serverStatus} />
                        <ConsoleOutput output={consoleOutput} />
                        <ConnectionStatus status={connectionStatus} />
                    </React.Fragment>
                );
            case 'config':
                return <ConfigPage />;
            case 'control':
                return <ControlPage />;
            case 'plugins':
                return <PluginsPage />;
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