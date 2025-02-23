const PluginsPage = () => {
    return (
        <div className="h-full flex flex-col">
            <h2 className="text-xl text-primary mb-4">Server Plugins</h2>
            <div className="flex-1 min-h-0">
                <div className="bg-surface rounded-lg p-4">
                    <div className="flex justify-between items-center mb-4">
                        <input 
                            type="text" 
                            placeholder="Search plugins..." 
                            className="bg-surface-lighter text-white p-2 rounded w-64"
                        />
                        <button className="bg-chardonnay-600 hover:bg-chardonnay-700 text-white px-4 py-2 rounded transition-colors">
                            Install New Plugin
                        </button>
                    </div>
                    <div className="text-neutral-400">
                        No plugins installed
                    </div>
                </div>
            </div>
        </div>
    );
}; 