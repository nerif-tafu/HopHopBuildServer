const ServerStatus = ({ status }) => {
    const [showRaw, setShowRaw] = React.useState(false);
    
    const statusColor = status.status === 'online' ? 'text-chardonnay-500' : 
                       status.status === 'offline' ? 'text-neutral-400' : 'text-yellow-500';

    return (
        <div className="bg-surface rounded-lg mb-4">
            <h3 
                onClick={() => setShowRaw(!showRaw)}
                className="text-xl text-primary m-0 flex justify-between items-center cursor-pointer select-none"
            >
                Server Status
                <span className="text-sm text-neutral-400">
                    {showRaw ? '▲ Hide Raw' : '▼ Show Raw'}
                </span>
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-2 my-2">
                <StatusItem label="Status" value={status.status.toUpperCase()} colorClass={statusColor} />
                <StatusItem label="Players" value={`${status.players}/${status.max_players}`} />
                <StatusItem label="FPS" value={status.fps} />
                <StatusItem label="Entities" value={status.entities} />
            </div>
            {showRaw && (
                <div className="mt-2 p-2 bg-neutral-800 rounded text-sm max-h-48 overflow-y-auto">
                    <pre className="m-0 whitespace-pre-wrap text-white">{status.raw || ''}</pre>
                </div>
            )}
        </div>
    );
}; 