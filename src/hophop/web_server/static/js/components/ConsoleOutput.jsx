const ConsoleOutput = ({ output }) => {
    const [rconCommand, setRconCommand] = React.useState('');
    const [isLoading, setIsLoading] = React.useState(false);
    const [isConnected, setIsConnected] = React.useState(false);
    const [toast, setToast] = React.useState(null);
    const [showSuggestions, setShowSuggestions] = React.useState(false);
    const consoleRef = React.useRef(null);
    const [commandHistory, setCommandHistory] = React.useState([]);
    const [historyIndex, setHistoryIndex] = React.useState(-1);

    const RCON_COMMANDS = [
        { command: 'admin.mutevoice', args: '"player"', desc: 'Prevent a player from speaking in-game' },
        { command: 'admin.unmutevoice', args: '"player"', desc: 'Allow a player to speak in-game' },
        { command: 'admin.mutechat', args: '"player"', desc: 'Prevent a player from sending messages via in-game chat' },
        { command: 'admin.unmutechat', args: '"player"', desc: 'Allow a player to send messages via in-game chat' },
        { command: 'chat.say', args: '"text"', desc: 'Prints your text in the chat' },
        { command: 'craft.add', args: '[id]', desc: 'Add an item to your crafting queue' },
        { command: 'craft.cancel', args: '[id]', desc: 'Cancel the crafting task of the specified item' },
        { command: 'craft.canceltask', desc: 'Cancel the item currently being crafted' },
        { command: 'entity.debug_lookat', desc: 'Enable debugging for the entity you are looking at' },
        { command: 'entity.find_entity', args: '[entity]', desc: 'Find the position of all entities with the provided name' },
        { command: 'entity.find_group', args: '[entity_group]', desc: 'Find the position of all entity groups with the provided name' },
        { command: 'entity.find_id', args: '[id]', desc: 'Find the position of an entity with the given ID' },
        { command: 'entity.find_parent', desc: 'Find the position of all parent entities' },
        { command: 'entity.find_radius', args: '[radius]', desc: 'Find the position of all entities in the given radius' },
        { command: 'entity.find_self', desc: 'Find the position of the player entity' },
        { command: 'entity.find_status', args: '[status]', desc: 'Find an entity with the given status' },
        { command: 'entity.spawn', args: '[entity]', desc: 'Spawn an entity where you are looking' },
        { command: 'entity.spawnat', args: '[entity] [worldPos]', desc: 'Spawn an entity at the specified position in the world' },
        { command: 'entity.spawnhere', args: '[entity] [distance]', desc: 'Spawn an entity nearby at the specified distance' },
        { command: 'entity.spawnitem', args: '[entity]', desc: 'Spawn an item in the world' },
        { command: 'env.addtime', args: '0-24', desc: 'Fast-forward time by the specified number of hours' },
        { command: 'gc.collect', desc: 'Collect the garbage dump' },
        { command: 'global.ban', args: '"player" "reason"', desc: 'Ban a player from the game (reason optional)' },
        { command: 'global.banid', args: '"player" "reason"', desc: 'Ban a player by Steam ID (reason optional)' },
        { command: 'global.banlist', desc: 'Displays a list of banned users' },
        { command: 'global.banlistex', desc: 'Displays a list of banned users with reasons and usernames' },
        { command: 'global.cleanup', desc: 'Cleanup command' },
        { command: 'global.colliders', desc: 'Colliders command' },
        { command: 'global.error', desc: 'Error command' },
        { command: 'global.injure', desc: 'Injure command' },
        { command: 'global.kick', args: '"player"', desc: 'Kick a player from the server' },
        { command: 'global.kickall', args: '"reason"', desc: 'Kick everyone from the game (reason optional)' },
        { command: 'global.kill', desc: 'Kill command' },
        { command: 'global.listid', desc: 'Displays a list of banned users by ID' },
        { command: 'global.moderatorid', args: '"id"', desc: 'Make a player a server moderator (AuthLevel 1)' },
        { command: 'global.objects', desc: 'Objects command' },
        { command: 'global.ownerid', args: '"id"', desc: 'Make a player a server owner (AuthLevel 2)' },
        { command: 'global.players', desc: 'Prints out currently connected players' },
        { command: 'global.queue', desc: 'Queue command' },
        { command: 'global.quit', desc: 'Leave the game' },
        { command: 'global.removemoderator', args: '"id"', desc: 'Remove a moderator' },
        { command: 'global.removeowner', args: '"id"', desc: 'Remove an owner' },
        { command: 'global.report', desc: 'Report command' },
        { command: 'global.respawn', desc: 'Respawn command' },
        { command: 'global.respawn_sleepingbag', desc: 'Respawn sleeping bag command' },
        { command: 'global.respawn_sleepingbag_remove', desc: 'Remove sleeping bag respawn' },
        { command: 'global.restart', desc: 'Restart server with 300 seconds warning' },
        { command: 'global.say', args: '"text"', desc: 'Sends a message to all players in chat' },
        { command: 'global.setinfo', desc: 'Set info command' },
        { command: 'global.sleep', desc: 'Sleep command' },
        { command: 'global.spectate', desc: 'Spectate command' },
        { command: 'global.status', desc: 'Show connected players and server stats (admin only)' },
        { command: 'global.teleport', desc: 'Teleport command' },
        { command: 'global.teleport2me', desc: 'Teleport to me command' },
        { command: 'global.teleportany', desc: 'Teleport any command' },
        { command: 'global.textures', desc: 'Textures command' },
        { command: 'global.unban', args: '"id"', desc: 'Unban a player from the game' },
        { command: 'global.users', desc: 'Shows user info for players on server' },
        { command: 'hierarchy.cd', desc: 'Change directory command' },
        { command: 'hierarchy.del', desc: 'Delete command' },
        { command: 'hierarchy.ls', desc: 'List command' },
        { command: 'inventory.endloot', desc: 'End loot command' },
        { command: 'inventory.give', desc: 'Give inventory command' },
        { command: 'inventory.giveall', desc: 'Give all inventory command' },
        { command: 'inventory.givearm', desc: 'Give arm command' },
        { command: 'inventory.givebp', desc: 'Give blueprint command' },
        { command: 'inventory.givebpall', desc: 'Give all blueprints command' },
        { command: 'inventory.giveid', desc: 'Give by ID command' },
        { command: 'inventory.giveto', desc: 'Give to player command' },
        { command: 'pool.clear', desc: 'Clear pool command' },
        { command: 'pool.status', desc: 'Pool status command' },
        { command: 'server.backup', desc: 'Backup the server folder' },
        { command: 'server.fill_groups', desc: 'Fill groups command' },
        { command: 'server.fill_populations', desc: 'Fill populations command' },
        { command: 'server.fps', desc: 'Show server FPS' },
        { command: 'server.readcfg', desc: 'Load server config' },
        { command: 'server.save', desc: 'Force a save-game' },
        { command: 'server.start', desc: 'Starts a server' },
        { command: 'server.stop', desc: 'Stops a server' },
        { command: 'server.writecfg', desc: 'Save all config changes' },
        { command: 'weather.clouds', desc: 'Weather clouds command' },
        { command: 'weather.fog', desc: 'Weather fog command' },
        { command: 'weather.rain', args: '[value]', desc: 'Set rain factor (0-1.0, auto if invalid)' },
        { command: 'weather.wind', desc: 'Weather wind command' }
    ];

    const getFilteredCommands = () => {
        if (!rconCommand) return [];
        return RCON_COMMANDS.filter(cmd => 
            cmd.command.toLowerCase().includes(rconCommand.toLowerCase())
        ).slice(0, 5); // Limit to 5 suggestions
    };

    const handleCommandSelect = (command) => {
        setRconCommand(command.command + (command.args ? ' ' : ''));
        setShowSuggestions(false);
    };

    const handleKeyDown = (e) => {
        if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (commandHistory.length > 0) {
                const newIndex = historyIndex + 1 >= commandHistory.length ? 0 : historyIndex + 1;
                setHistoryIndex(newIndex);
                setRconCommand(commandHistory[commandHistory.length - 1 - newIndex]);
            }
        } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (commandHistory.length > 0) {
                if (historyIndex <= 0) {
                    setHistoryIndex(-1);
                    setRconCommand('');
                } else {
                    const newIndex = historyIndex - 1;
                    setHistoryIndex(newIndex);
                    setRconCommand(commandHistory[commandHistory.length - 1 - newIndex]);
                }
            }
        }
    };

    React.useEffect(() => {
        // Check if we're connected by looking for specific messages in the output
        setIsConnected(output.includes('Server startup complete'));

        // Auto-scroll to bottom when output changes
        if (consoleRef.current) {
            consoleRef.current.scrollTop = consoleRef.current.scrollHeight;
        }
    }, [output]);

    const showToast = (message, type = 'info') => {
        setToast({ message, type });
    };

    const handleRconSubmit = async (e) => {
        e.preventDefault();
        if (!rconCommand.trim()) return;

        // Add command to history
        setCommandHistory(prev => [...prev, rconCommand]);
        setHistoryIndex(-1);

        setIsLoading(true);
        try {
            const response = await fetch('/api/rcon', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ command: rconCommand })
            });

            if (!response.ok) throw new Error('Failed to execute command');
            
            const data = await response.json();
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                console.log(data);
                const formattedResponse = formatRconResponse(data.response);
                showToast(`> ${rconCommand}\n${formattedResponse}`, 'success');
                setRconCommand('');
            }
        } catch (error) {
            showToast('Failed to execute command', 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const formatRconResponse = (response) => {
        console.log(response);
        if (!response) return 'Command executed successfully';
        try {
            // Handle WebSocket-style responses
            if (response.Message) {
                // If it's a table-style response (like player list)
                if (typeof response.Message === 'string' && response.Message.includes('\t')) {
                    // Split into lines and format as table
                    const lines = response.Message.split('\n').filter(line => line.trim());
                    if (lines.length > 0) {
                        // Format each line, replacing multiple spaces/tabs with a single space
                        return lines.map(line => 
                            line.replace(/\s+/g, ' ').trim()
                        ).join('\n');
                    }
                }
                
                if (Array.isArray(response.Message)) {
                    return response.Message.map(player => {
                        if (typeof player === 'object') {
                            return `${player.SteamID} - ${player.DisplayName} (${player.Ping}ms)`;
                        }
                        return player;
                    }).join('\n');
                }
                return response.Message;
            }
            
            // Try to parse as JSON if it's a string
            if (typeof response === 'string') {
                // Check if it's a table-style response first
                if (response.includes('\t')) {
                    const lines = response.split('\n').filter(line => line.trim());
                    if (lines.length > 0) {
                        return lines.map(line => 
                            line.replace(/\s+/g, ' ').trim()
                        ).join('\n');
                    }
                }

                try {
                    const parsed = JSON.parse(response);
                    if (Array.isArray(parsed)) {
                        return parsed.map(msg => msg.Message || msg).join('\n');
                    }
                    return JSON.stringify(parsed, null, 2);
                } catch (err) {
                    return response;
                }
            }
            
            return response;
        } catch (error) {
            // If not JSON or other format, return as is
            return response;
        }
    };

    return (
        <div className="flex-1 flex flex-col min-h-0">
            {/* Console Output */}
            <div 
                ref={consoleRef}
                className="flex-1 bg-neutral-800 rounded p-2 mb-4 overflow-y-auto font-mono text-sm"
            >
                <pre className="text-white whitespace-pre-wrap break-words overflow-x-hidden w-full">{output}</pre>
            </div>

            {/* RCON Command Input */}
            <div className="bg-surface rounded-lg">
                <form onSubmit={handleRconSubmit} className="flex gap-2">
                    <div className="flex-1 relative">
                        <input
                            type="text"
                            value={rconCommand}
                            onChange={(e) => {
                                setRconCommand(e.target.value);
                                setShowSuggestions(true);
                            }}
                            onKeyDown={handleKeyDown}
                            onFocus={() => setShowSuggestions(true)}
                            onBlur={() => setTimeout(() => setShowSuggestions(false), 200)}
                            placeholder="Enter RCON command..."
                            className={`w-full bg-surface-light p-2 rounded text-neutral-900 placeholder-neutral-400
                                ${!isConnected ? 'opacity-50 cursor-not-allowed' : ''}`}
                            disabled={!isConnected || isLoading}
                        />
                        {(!isConnected || isLoading) && (
                            <div className="absolute inset-0 flex items-center justify-center bg-surface-light bg-opacity-90 rounded">
                                <span className="text-neutral-500 text-sm">
                                    {!isConnected ? 'Waiting for server...' : 'Sending command...'}
                                </span>
                            </div>
                        )}
                        
                        {/* Command Suggestions */}
                        {showSuggestions && rconCommand && (
                            <div className="absolute bottom-full left-0 right-0 mb-1 bg-surface-light rounded shadow-lg z-10 max-h-60 overflow-y-auto">
                                {getFilteredCommands().map((cmd, index) => (
                                    <div
                                        key={cmd.command}
                                        className="p-2 hover:bg-surface cursor-pointer border-b border-surface-lighter last:border-0"
                                        onClick={() => handleCommandSelect(cmd)}
                                    >
                                        <div className="font-mono text-sm text-neutral-900">
                                            {cmd.command} {cmd.args}
                                        </div>
                                        <div className="text-xs text-neutral-500">{cmd.desc}</div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                    <button
                        type="submit"
                        disabled={!isConnected || isLoading || !rconCommand.trim()}
                        className={`px-4 py-2 rounded transition-colors ${
                            isConnected && !isLoading && rconCommand.trim()
                                ? 'bg-chardonnay-600 hover:bg-chardonnay-700 text-white cursor-pointer'
                                : 'bg-neutral-300 text-neutral-500 cursor-not-allowed'
                        }`}
                    >
                        {isLoading ? 'Sending...' : 'Send'}
                    </button>
                </form>
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

window.ConsoleOutput = ConsoleOutput; 