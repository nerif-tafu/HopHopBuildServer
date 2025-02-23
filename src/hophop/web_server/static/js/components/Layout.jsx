const Layout = ({ children, currentPage, onPageChange }) => {
    const navItems = [
        { 
            id: 'status', 
            label: 'Status', 
            icon: <i className="fas fa-chart-bar text-xl" />
        },
        { 
            id: 'config', 
            label: 'Config', 
            icon: <i className="fas fa-cog text-xl" />
        },
        { 
            id: 'control', 
            label: 'Control', 
            icon: <i className="fas fa-terminal text-xl" />
        },
        { 
            id: 'plugins', 
            label: 'Plugins', 
            icon: <i className="fas fa-puzzle-piece text-xl" />
        }
    ];

    return (
        <div className="h-full flex">
            {/* Sidebar */}
            <nav className="bg-surface-light flex flex-col border-r border-surface-lighter transition-all duration-300
                w-16 md:w-64"
            >
                <div className="p-4 overflow-hidden">
                    <span className="text-xl font-bold text-chardonnay-500 block md:hidden flex items-center justify-center">
                        <i className="fas fa-desktop text-xl" />
                    </span>
                    <h1 className="text-xl font-bold text-chardonnay-500 hidden md:block">Rust Server Console</h1>
                </div>
                <div className="space-y-2 p-2">
                    {navItems.map(item => (
                        <button
                            key={item.id}
                            onClick={() => onPageChange(item.id)}
                            title={item.label}
                            className={`w-full h-10 px-4 py-2 rounded flex items-center transition-colors
                                ${currentPage === item.id 
                                    ? 'bg-chardonnay-600 text-white' 
                                    : 'text-neutral-400 hover:bg-surface-lighter hover:text-chardonnay-300'}`}
                        >
                            <div className="w-full flex items-center justify-center md:justify-start">
                                {item.icon}
                                <span className="hidden md:inline ml-3">{item.label}</span>
                            </div>
                        </button>
                    ))}
                </div>
            </nav>

            {/* Main Content */}
            <main className="flex-1 p-4 md:p-6 flex flex-col min-h-0 bg-surface">
                {children}
            </main>
        </div>
    );
};

window.Layout = Layout; 