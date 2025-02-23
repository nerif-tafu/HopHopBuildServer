const ControlPage = () => {
    return (
        <div className="flex flex-col gap-4">
            <h2 className="text-xl text-primary">Server Control</h2>
            <div className="bg-surface rounded-lg">
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                    <button className="bg-chardonnay-600 hover:bg-chardonnay-700 text-white p-3 rounded transition-colors">
                        Restart Server
                    </button>
                    <button className="bg-chardonnay-600 hover:bg-chardonnay-700 text-white p-3 rounded transition-colors">
                        Stop Server
                    </button>
                    <button className="bg-chardonnay-600 hover:bg-chardonnay-700 text-white p-3 rounded transition-colors">
                        Update Server
                    </button>
                </div>
            </div>
        </div>
    );
}; 