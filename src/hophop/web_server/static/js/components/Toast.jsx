const Toast = ({ message, type, onClose }) => {
    React.useEffect(() => {
        const timer = setTimeout(() => {
            onClose();
        }, 5000);  // Increased to 5 seconds to give more time to read errors

        return () => clearTimeout(timer);
    }, [onClose]);

    const getTypeStyles = () => {
        switch (type) {
            case 'success':
                return 'bg-status-success text-white';
            case 'error':
                return 'bg-status-error text-white';
            default:
                return 'bg-status-unknown text-white';
        }
    };

    return (
        <div className={`fixed bottom-4 right-4 px-4 py-3 rounded-lg shadow-lg flex items-start max-w-md ${getTypeStyles()}`}>
            <div className="flex-1">{message}</div>
            <button 
                onClick={onClose}
                className="ml-3 text-white hover:text-gray-200 self-start"
            >
                ×
            </button>
        </div>
    );
};

window.Toast = Toast; 