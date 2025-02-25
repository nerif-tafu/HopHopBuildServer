return (
    <div className="h-full flex flex-col">
        <div className="mb-6">
            <h2 className="text-xl text-primary">Server Status</h2>
        </div>
        <div className="flex-1 min-h-0">
            <div className="bg-surface-light rounded-lg p-4 h-full flex flex-col">
                <div className="space-y-3">
                    {/* Status sections */}
                    <div className="p-4 bg-surface rounded-lg border border-surface-lighter">
                        <h3 className="text-lg text-primary mb-2">System Status</h3>
                        <div className="bg-neutral-800 rounded-lg p-4 font-mono text-sm text-white">
                            {/* System status content */}
                        </div>
                    </div>

                    <div className="p-4 bg-surface rounded-lg border border-surface-lighter">
                        <h3 className="text-lg text-primary mb-2">Performance Metrics</h3>
                        <div className="bg-neutral-800 rounded-lg p-4 font-mono text-sm text-white">
                            {/* Performance metrics content */}
                        </div>
                    </div>

                    <div className="p-4 bg-surface rounded-lg border border-surface-lighter">
                        <h3 className="text-lg text-primary mb-2">Active Players</h3>
                        <div className="bg-neutral-800 rounded-lg p-4 font-mono text-sm text-white 
                            scrollbar-thin scrollbar-thumb-neutral-600 scrollbar-track-neutral-800 
                            hover:scrollbar-thumb-neutral-500"
                        >
                            {/* Players list content */}
                        </div>
                    </div>
                </div>
            </div>
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