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

    const renderConfigField = (key, defaultValue) => {
        const error = errors[key];
        const fieldClasses = `bg-surface p-2 rounded text-neutral-900 md:col-span-2 placeholder-neutral-400 
            ${error ? 'border-status-error' : 'border-surface-lighter'}`;

        if (key === 'RUST_BRANCH') {
            return (
                <div className="md:col-span-2">
                    <select
                        value={config.current[key] || defaultValue}
                        onChange={(e) => handleConfigChange(key, e.target.value)}
                        className={fieldClasses}
                    >
                        {RUST_BRANCHES.map(branch => (
                            <option key={branch.value} value={branch.value}>
                                {branch.label}
                            </option>
                        ))}
                    </select>
                    {error && <div className="text-status-error text-sm mt-1">{error}</div>}
                </div>
            );
        }

        return (
            <div className="md:col-span-2">
                <input
                    type={['SERVER_PORT', 'SERVER_RCON_PORT', 'SERVER_SEED', 'SERVER_WORLDSIZE', 'SERVER_MAXPLAYERS'].includes(key) ? 'number' : 'text'}
                    value={config.current[key] || ''}
                    onChange={(e) => handleConfigChange(key, e.target.value)}
                    placeholder={defaultValue}
                    className={fieldClasses}
                />
                {error && <div className="text-status-error text-sm mt-1">{error}</div>}
            </div>
        );
    };

    const renderConfigSection = () => (
        <div className="bg-surface-light rounded-lg p-4 mb-4">
            <h3 className="text-lg text-chardonnay-500 mb-4">Configuration</h3>
            <div className="grid gap-4">
                {Object.entries(config.defaults).map(([key, defaultValue]) => (
                    <div key={key} className="grid grid-cols-1 md:grid-cols-3 gap-4 items-center">
                        <label className="text-neutral-600 font-medium">{key}</label>
                        {renderConfigField(key, defaultValue)}
                    </div>
                ))}
            </div>
        </div>
    );

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
            <div className="flex flex-col gap-4">
                <h2 className="text-xl text-primary">Server Configuration</h2>
                <div className="bg-surface rounded-lg p-4">
                    <p className="text-neutral-400">Loading configuration...</p>
                </div>
            </div>
        );
    }

    if (config.error) {
        return (
            <div className="flex flex-col gap-4">
                <h2 className="text-xl text-primary">Server Configuration</h2>
                <div className="bg-surface rounded-lg p-4">
                    <p className="text-status-error">{config.error}</p>
                </div>
            </div>
        );
    }

    return (
        <div className="flex flex-col gap-4">
            <div className="flex justify-between items-center">
                <h2 className="text-xl text-primary">Server Configuration</h2>
                <button 
                    onClick={handleSave}
                    disabled={!hasChanges}
                    className={`px-4 py-2 rounded transition-colors ${
                        hasChanges 
                            ? 'bg-chardonnay-600 hover:bg-chardonnay-700 text-white cursor-pointer' 
                            : 'bg-neutral-300 text-neutral-500 cursor-not-allowed'
                    }`}
                >
                    Save Changes
                </button>
            </div>
            
            {renderConfigSection()}
            
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