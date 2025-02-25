const ControlPage = () => {
    return (
        <div className="h-full flex flex-col">
            <div className="mb-6">
                <h2 className="text-xl text-primary">Server Control</h2>
            </div>
            <div className="flex-1 min-h-0">
                <ServerControl />
            </div>
        </div>
    );
}; 