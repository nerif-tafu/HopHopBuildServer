const ControlPage = () => {
    return (
        <div className="h-full flex flex-col">
            <h2 className="text-xl text-primary mb-4">Server Control</h2>
            <div className="flex-1 min-h-0">
                <ServerControl />
            </div>
        </div>
    );
}; 