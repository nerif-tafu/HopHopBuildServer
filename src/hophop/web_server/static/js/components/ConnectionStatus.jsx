const ConnectionStatus = ({ state, message }) => {
    const getStatusColor = () => {
        switch (state) {
            case 'online': return 'text-status-success';
            case 'offline': return 'text-status-error';
            case 'connected': return 'text-status-success';
            case 'disconnected': return 'text-status-error';
            default: return 'text-status-unknown';
        }
    };

    return (
        <div className={`flex-shrink-0 italic text-sm p-2 text-left border-t border-surface-lighter ${getStatusColor()}`}>
            {message}
        </div>
    );
}; 