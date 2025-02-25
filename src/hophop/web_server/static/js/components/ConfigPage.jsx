const ConfigPage = () => {
    const [config, setConfig] = React.useState({
        current: {},
        defaults: {},
        loading: true,
        error: null
    });
    const [originalConfig, setOriginalConfig] = React.useState({});
    const [errors, setErrors] = React.useState({});
    const [toast, setToast] = React.useState(null);
    const [hasChanges, setHasChanges] = React.useState(false);

    const validateField = (key, value) => {
        const validator = window.configValidation[key];
        if (validator) {
            // Skip validation if field is optional and empty
            if (validator.optional && !value) {
                setErrors(prev => ({ ...prev, [key]: null }));
                return null;
            }
            const error = validator.validate(value);
            setErrors(prev => ({ ...prev, [key]: error }));
            return error;
        }
        return null;
    };

    React.useEffect(() => {
        fetchConfig();
    }, []);

    const fetchConfig = async () => {
        try {
            const response = await fetch('/api/config');
            const data = await response.json();
            setConfig({
                current: data.current || {},
                defaults: data.defaults || {},
                loading: false,
                error: null
            });
            setOriginalConfig(data.current || {});
            setHasChanges(false);
        } catch (err) {
            setConfig(prev => ({
                ...prev,
                loading: false,
                error: 'Failed to load configuration'
            }));
            showToast('Failed to load configuration', 'error');
        }
    };

    const showToast = (message, type = 'info') => {
        setToast({ message, type });
    };

    const RUST_BRANCHES = [
        { value: 'master', label: 'Master (Stable)' },
        { value: 'staging', label: 'Staging (Beta)' },
        { value: 'aux03', label: 'Aux03 (Testing)' },
        { value: 'aux02', label: 'Aux02 (Testing)' },
        { value: 'aux01', label: 'Aux01 (Testing)' },
        { value: 'edge', label: 'Edge (Latest)' },
        { value: 'preview', label: 'Preview (Experimental)' }
    ];

    const renderConfigField = (key) => {
        const currentValue = config.current[key] || '';
        const defaultValue = config.defaults[key] || '';
        const error = errors[key];

        // Special case for RUST_BRANCH to use a dropdown
        if (key === 'RUST_BRANCH') {
            return (
                <div key={key} className="flex flex-col md:flex-row md:items-center p-4 border-b border-surface-lighter">
                    <label className="mb-2 md:mb-0 md:w-1/3 text-primary font-medium">{key}</label>
                    <div className="w-full md:w-2/3">
                        <select
                            value={currentValue}
                            onChange={(e) => handleConfigChange(key, e.target.value)}
                            className="w-full p-2 bg-neutral-700 rounded border border-neutral-600 
                                text-white focus:outline-none focus:border-chardonnay-500"
                        >
                            {RUST_BRANCHES.map(branch => (
                                <option key={branch.value} value={branch.value}>
                                    {branch.label}
                                </option>
                            ))}
                        </select>
                        {error && <p className="mt-1 text-sm text-status-error">{error}</p>}
                        {defaultValue !== currentValue && (
                            <p className="mt-1 text-sm text-neutral-500">
                                Default: {defaultValue}
                            </p>
                        )}
                    </div>
                </div>
            );
        }

        // Regular input field
        return (
            <div key={key} className="flex flex-col md:flex-row md:items-center p-4 border-b border-surface-lighter">
                <label className="mb-2 md:mb-0 md:w-1/3 text-primary font-medium">{key}</label>
                <div className="w-full md:w-2/3">
                    <input
                        type="text"
                        value={currentValue}
                        onChange={(e) => handleConfigChange(key, e.target.value)}
                        className={`w-full p-2 bg-neutral-700 rounded border 
                            ${error ? 'border-status-error' : 'border-neutral-600'} 
                            text-white focus:outline-none focus:border-chardonnay-500`}
                    />
                    {error && <p className="mt-1 text-sm text-status-error">{error}</p>}
                    {defaultValue !== currentValue && (
                        <p className="mt-1 text-sm text-neutral-500">
                            Default: {defaultValue}
                        </p>
                    )}
                </div>
            </div>
        );
    };

    const renderConfigSection = () => {
        if (config.loading) {
            return (
                <div className="p-4 text-center text-neutral-500">
                    Loading configuration...
                </div>
            );
        }

        if (config.error) {
            return (
                <div className="p-4 text-status-error">
                    {config.error}
                </div>
            );
        }

        return (
            <div className="h-full flex flex-col">
                <div className="flex-1 overflow-y-auto scrollbar-thin scrollbar-thumb-neutral-600 
                    scrollbar-track-neutral-800 hover:scrollbar-thumb-neutral-500">
                    {Object.keys(config.defaults).map(key => renderConfigField(key))}
                </div>
            </div>
        );
    };

    const handleConfigChange = (key, value) => {
        // For number fields, convert string to number for validation
        const processedValue = ['SERVER_PORT', 'SERVER_RCON_PORT', 'SERVER_SEED', 'SERVER_WORLDSIZE', 'SERVER_MAXPLAYERS']
            .includes(key) ? Number(value) : value;

        validateField(key, processedValue);
        
        const newConfig = {
            ...config.current,
            [key]: processedValue
        };
        
        setConfig(prev => ({
            ...prev,
            current: newConfig
        }));

        // Check if the new value is different from the original
        const hasAnyChanges = Object.entries(newConfig).some(([k, v]) => {
            // Handle both string and number comparisons
            return String(v) !== String(originalConfig[k]);
        });
        
        setHasChanges(hasAnyChanges);
    };

    const handleSave = async () => {
        // Validate all fields
        const newErrors = {};
        const errorMessages = [];

        // Get all fields from both validation schema and current config
        const fieldsToValidate = new Set([
            ...Object.keys(window.configValidation),
            ...Object.keys(config.current)
        ]);

        await Promise.all([...fieldsToValidate].map(async (key) => {
            const value = config.current[key];
            const error = validateField(key, value);
            if (error) {
                newErrors[key] = error;
                const readableKey = key.replace(/_/g, ' ').toLowerCase();
                errorMessages.push(`${readableKey}: ${error}`);
            }
        }));

        setErrors(newErrors);

        if (errorMessages.length > 0) {
            showToast(
                <div>
                    <div>Please fix the following errors:</div>
                    {errorMessages.map((msg, index) => (
                        <div key={index} className="mt-1 text-sm">• {msg}</div>
                    ))}
                </div>,
                'error'
            );
            return;
        }

        try {
            const response = await fetch('/api/config', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(config.current)
            });
            
            if (!response.ok) throw new Error('Failed to save configuration');
            
            showToast('Configuration saved successfully', 'success');
            await fetchConfig();
        } catch (err) {
            showToast('Failed to save configuration', 'error');
        }
    };

    if (config.loading) {
        return (
            <div className="h-full flex flex-col">
                <div className="mb-6">
                    <h2 className="text-xl text-primary">Server Configuration</h2>
                </div>
                <div className="flex-1 min-h-0">
                    <div className="bg-surface rounded-lg p-4">
                        <p className="text-neutral-400">Loading configuration...</p>
                    </div>
                </div>
            </div>
        );
    }

    if (config.error) {
        return (
            <div className="h-full flex flex-col">
                <div className="mb-6">
                    <h2 className="text-xl text-primary">Server Configuration</h2>
                </div>
                <div className="flex-1 min-h-0">
                    <div className="bg-surface rounded-lg p-4">
                        <p className="text-status-error">{config.error}</p>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="h-full flex flex-col">
            <div className="mb-6">
                <h2 className="text-xl text-primary">Server Configuration</h2>
            </div>
            <div className="flex-1 min-h-0">
                <div className="bg-surface-light rounded-lg h-full">
                    {renderConfigSection()}
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
};

window.ConfigPage = ConfigPage; 