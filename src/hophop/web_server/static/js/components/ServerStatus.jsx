const ServerStatus = ({ status }) => {
    const statusColor = status.status === 'online' ? 'text-status-success' : 
                       status.status === 'offline' ? 'text-status-error' : 'text-status-unknown';

    // Make status globally available for ConsoleOutput
    window.serverStatus = status;

    // Make sure ConsoleOutput is available
    const ConsoleOutput = window.ConsoleOutput;

    return (
        <div className="h-full flex flex-col space-y-3">
            {/* Status sections */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="bg-surface p-4 border border-surface-lighter rounded-lg">
                    <div className="flex flex-col">
                        <span className="text-neutral-400 text-sm mb-2">Status</span>
                        <span className={`${statusColor} text-xl font-semibold`}>
                            {status.status.toUpperCase()}
                        </span>
                    </div>
                </div>
                
                <div className="bg-surface p-4 border border-surface-lighter rounded-lg">
                    <div className="flex flex-col">
                        <span className="text-neutral-400 text-sm mb-2">Players</span>
                        <span className="text-neutral-700 text-xl font-semibold">
                            {`${status.players}/${status.max_players}`}
                        </span>
                    </div>
                </div>

                <div className="bg-surface p-4 border border-surface-lighter rounded-lg">
                    <div className="flex flex-col">
                        <span className="text-neutral-400 text-sm mb-2">FPS</span>
                        <span className="text-neutral-700 text-xl font-semibold">
                            {status.fps}
                        </span>
                    </div>
                </div>

                <div className="bg-surface p-4 border border-surface-lighter rounded-lg">
                    <div className="flex flex-col">
                        <span className="text-neutral-400 text-sm mb-2">Entities</span>
                        <span className="text-neutral-700 text-xl font-semibold">
                            {status.entities}
                        </span>
                    </div>
                </div>
            </div>

            {/* Console Output with RCON */}
            <div className="flex-1 min-h-0">
                {ConsoleOutput && <ConsoleOutput output={status.console || ''} />}
            </div>
        </div>
    );
};

window.ServerStatus = ServerStatus; 