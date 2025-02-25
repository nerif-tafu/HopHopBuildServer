const PluginsPage = () => {
    const [plugins, setPlugins] = React.useState([]);
    const [isLoading, setIsLoading] = React.useState(false);
    const [toast, setToast] = React.useState(null);
    const [selectedPlugin, setSelectedPlugin] = React.useState(null);
    const [editorContent, setEditorContent] = React.useState('');
    const [originalContent, setOriginalContent] = React.useState('');
    const [selectedFileType, setSelectedFileType] = React.useState('code');
    const fileInputRef = React.useRef();
    const containerRef = React.useRef(null);
    const [view, setView] = React.useState(null);

    React.useEffect(() => {
        fetchPlugins();
    }, []);

    const fetchPlugins = async () => {
        try {
            const response = await fetch('/api/plugins');
            const data = await response.json();
            if (data.error) throw new Error(data.error);
            setPlugins(data.plugins);
        } catch (error) {
            showToast(error.message, 'error');
        }
    };

    const fetchPluginContent = async (plugin, fileType = 'code') => {
        try {
            const response = await fetch(`/api/plugins/${encodeURIComponent(plugin.name)}/${fileType}`);
            const data = await response.json();
            if (data.error) throw new Error(data.error);
            setEditorContent(data.content);
            setOriginalContent(data.content);
            setSelectedPlugin(plugin);
            setSelectedFileType(fileType);
        } catch (error) {
            showToast(error.message, 'error');
        }
    };

    const savePluginContent = async () => {
        if (!selectedPlugin) return;
        
        setIsLoading(true);
        try {
            const response = await fetch('/api/plugins/content', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: selectedPlugin.name,
                    content: editorContent,
                    type: selectedFileType
                })
            });
            const data = await response.json();
            if (data.error) throw new Error(data.error);
            setOriginalContent(editorContent);
            showToast('File saved successfully', 'success');
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const handleUpload = async (event) => {
        event.preventDefault();
        const file = fileInputRef.current.files[0];
        if (!file) {
            showToast('Please select a file', 'error');
            return;
        }

        const formData = new FormData();
        formData.append('file', file);

        setIsLoading(true);
        try {
            const response = await fetch('/api/plugins/upload', {
                method: 'POST',
                body: formData
            });
            const data = await response.json();
            
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                showToast('Plugin uploaded successfully', 'success');
                setPlugins([...plugins, data.plugin]);
                fileInputRef.current.value = '';
            }
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const togglePlugin = async (plugin) => {
        setIsLoading(true);
        try {
            const response = await fetch('/api/plugins/toggle', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: plugin.name,
                    activate: !plugin.active
                })
            });
            const data = await response.json();
            
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                showToast(data.message, 'success');
                setPlugins(plugins.map(p => 
                    p.name === plugin.name 
                        ? {...p, active: data.active}
                        : p
                ));
            }
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const deletePlugin = async (plugin) => {
        if (!window.confirm(`Are you sure you want to delete ${plugin.name}?`)) {
            return;
        }

        setIsLoading(true);
        try {
            const response = await fetch('/api/plugins/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: plugin.name })
            });
            const data = await response.json();
            
            if (data.error) {
                showToast(data.error, 'error');
            } else {
                showToast('Plugin deleted successfully', 'success');
                setPlugins(plugins.filter(p => p.name !== plugin.name));
                if (selectedPlugin && selectedPlugin.name === plugin.name) {
                    setSelectedPlugin(null);
                }
            }
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            setIsLoading(false);
        }
    };

    const showToast = (message, type = 'info') => {
        setToast({ message, type });
    };

    const isContentModified = () => {
        return editorContent !== originalContent;
    };

    const handleCopyToClipboard = async () => {
        try {
            await navigator.clipboard.writeText(editorContent);
            showToast('Content copied to clipboard', 'success');
        } catch (error) {
            showToast('Failed to copy to clipboard', 'error');
        }
    };

    const handleDownload = () => {
        try {
            const extension = selectedFileType === 'code' ? '.cs' : '.json';
            const filename = `${selectedPlugin.name}${extension}`;
            const blob = new Blob([editorContent], { type: 'text/plain' });
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
        } catch (error) {
            showToast('Failed to download file', 'error');
        }
    };

    const getLanguageFromFileType = (fileType) => {
        switch (fileType) {
            case 'code':
                return 'csharp';
            case 'config':
                return 'json';
            default:
                return 'plaintext';
        }
    };

    const renderPluginActions = (plugin) => (
        <div className="flex items-center gap-2">
            <button
                onClick={() => fetchPluginContent(plugin, 'code')}
                className="px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                    transition-colors flex items-center gap-1.5"
            >
                <i className="fas fa-code"></i>
                Edit Code
            </button>
            
            {plugin.hasConfig && (
                <button
                    onClick={() => fetchPluginContent(plugin, 'config')}
                    className="px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                        transition-colors flex items-center gap-1.5"
                >
                    <i className="fas fa-cog"></i>
                    Config
                </button>
            )}
            
            {plugin.hasData && (
                <button
                    onClick={() => fetchPluginContent(plugin, 'data')}
                    className="px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                        transition-colors flex items-center gap-1.5"
                >
                    <i className="fas fa-database"></i>
                    Data
                </button>
            )}
            
            {plugin.hasLang && (
                <button
                    onClick={() => fetchPluginContent(plugin, 'lang')}
                    className="px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                        transition-colors flex items-center gap-1.5"
                >
                    <i className="fas fa-language"></i>
                    Language
                </button>
            )}

            <button
                onClick={() => togglePlugin(plugin)}
                disabled={isLoading}
                className={`px-3 py-1.5 text-sm rounded text-white disabled:opacity-50 
                    transition-colors flex items-center gap-1.5
                    ${plugin.active 
                        ? 'bg-green-500 hover:bg-green-600' 
                        : 'bg-red-500 hover:bg-red-600'
                    }`}
            >
                <i className={`fas ${plugin.active ? 'fa-toggle-on' : 'fa-toggle-off'}`}></i>
                {plugin.active ? 'Enabled' : 'Disabled'}
            </button>

            <button
                onClick={() => deletePlugin(plugin)}
                className="px-3 py-1.5 text-sm rounded bg-red-500 hover:bg-red-600 
                    text-white transition-colors flex items-center gap-1.5"
            >
                <i className="fas fa-trash"></i>
                Delete
            </button>
        </div>
    );

    const getEditorTitle = () => {
        if (!selectedPlugin) return '';
        const types = {
            code: 'Code',
            config: 'Config',
            data: 'Data',
            lang: 'Language'
        };
        return `${selectedPlugin.name} - ${types[selectedFileType]}`;
    };

    React.useEffect(() => {
        if (!selectedPlugin || !window.CodeMirror) return;
        
        const container = document.getElementById('codemirror-container');
        if (!container) return;

        const editor = window.CodeMirror(container, {
            value: editorContent,
            mode: selectedFileType === 'code' ? 'text/x-csharp' : 'application/json',
            theme: 'dracula',
            lineNumbers: true,
            autofocus: true,
            tabSize: 4,
            indentUnit: 4,
            lineWrapping: true,
            matchBrackets: true,
            autoCloseBrackets: true,
            styleActiveLine: true,
            viewportMargin: Infinity,
            height: "auto"
        });

        // Make editor responsive
        const resizeEditor = () => {
            editor.setSize("100%", "100%");
        };

        // Initial size
        resizeEditor();

        // Handle window resizes
        window.addEventListener('resize', resizeEditor);

        editor.on('change', (cm) => {
            setEditorContent(cm.getValue());
        });

        // Store editor instance for cleanup
        setView(editor);

        // Cleanup
        return () => {
            window.removeEventListener('resize', resizeEditor);
            container.innerHTML = '';
        };
    }, [selectedPlugin, selectedFileType]);

    return (
        <div className="h-full flex flex-col">
            <h2 className="text-xl text-primary mb-4">Server Plugins</h2>
            <div className="flex-1 min-h-0">
                <div className="bg-surface rounded-lg p-4">
                    {/* Upload section */}
                    <div className="mb-6 p-4 border-2 border-dashed border-surface-lighter rounded-lg bg-surface-light">
                        <h3 className="text-lg mb-2 text-primary">Install New Plugin</h3>
                        <form onSubmit={handleUpload} className="flex gap-2">
                            <div className="flex-1 relative">
                                <input
                                    type="file"
                                    accept=".cs"
                                    ref={fileInputRef}
                                    className="block w-full text-sm text-neutral-900
                                        file:mr-4 file:py-2 file:px-4
                                        file:rounded file:border-0
                                        file:text-sm file:font-semibold
                                        file:bg-chardonnay-500 file:text-white
                                        hover:file:bg-chardonnay-600
                                        file:cursor-pointer file:transition-colors
                                        disabled:opacity-50 disabled:cursor-not-allowed"
                                    disabled={isLoading}
                                />
                            </div>
                            <button
                                type="submit"
                                disabled={isLoading}
                                className="px-4 py-2 rounded bg-chardonnay-500 text-white 
                                    hover:bg-chardonnay-600 transition-colors 
                                    disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {isLoading ? (
                                    <span className="flex items-center">
                                        <i className="fas fa-spinner fa-spin mr-2"></i>
                                        Uploading...
                                    </span>
                                ) : (
                                    <span className="flex items-center">
                                        <i className="fas fa-upload mr-2"></i>
                                        Upload
                                    </span>
                                )}
                            </button>
                        </form>
                    </div>

                    {/* Plugins list */}
                    <div className="space-y-2">
                        {plugins.map(plugin => (
                            <div key={plugin.name} 
                                className="flex flex-col sm:flex-row sm:items-center justify-between p-3 bg-surface-light rounded 
                                    hover:bg-surface-lighter transition-colors gap-3"
                            >
                                {/* Plugin name */}
                                <div className="font-mono text-sm">
                                    {plugin.name}
                                </div>

                                {/* Actions */}
                                <div className="flex flex-wrap gap-2">
                                    <button
                                        onClick={() => fetchPluginContent(plugin, 'code')}
                                        className="flex-1 sm:flex-none px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                                            transition-colors flex items-center justify-center gap-1.5"
                                    >
                                        <i className="fas fa-code"></i>
                                        <span className="sm:hidden md:inline">Code</span>
                                    </button>
                                    
                                    {plugin.hasConfig && (
                                        <button
                                            onClick={() => fetchPluginContent(plugin, 'config')}
                                            className="flex-1 sm:flex-none px-3 py-1.5 text-sm rounded bg-surface hover:bg-neutral-200 
                                                transition-colors flex items-center justify-center gap-1.5"
                                        >
                                            <i className="fas fa-cog"></i>
                                            <span className="sm:hidden md:inline">Config</span>
                                        </button>
                                    )}
                                    
                                    <button
                                        onClick={() => togglePlugin(plugin)}
                                        disabled={isLoading}
                                        className={`flex-1 sm:flex-none px-3 py-1.5 text-sm rounded text-white disabled:opacity-50 
                                            transition-colors flex items-center justify-center gap-1.5
                                            ${plugin.active 
                                                ? 'bg-green-500 hover:bg-green-600' 
                                                : 'bg-red-500 hover:bg-red-600'
                                            }`}
                                    >
                                        <i className={`fas ${plugin.active ? 'fa-toggle-on' : 'fa-toggle-off'}`}></i>
                                        <span className="sm:hidden md:inline">{plugin.active ? 'Enabled' : 'Disabled'}</span>
                                    </button>

                                    <button
                                        onClick={() => deletePlugin(plugin)}
                                        className="flex-1 sm:flex-none px-3 py-1.5 text-sm rounded bg-red-500 hover:bg-red-600 
                                            text-white transition-colors flex items-center justify-center gap-1.5"
                                    >
                                        <i className="fas fa-trash"></i>
                                        <span className="sm:hidden md:inline">Delete</span>
                                    </button>
                                </div>
                            </div>
                        ))}
                        {plugins.length === 0 && (
                            <div className="text-center text-neutral-500 py-8">
                                <i className="fas fa-puzzle-piece text-4xl mb-2"></i>
                                <p>No plugins available</p>
                                <p className="text-sm mt-1">Upload a .cs file to get started</p>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {/* Editor Modal */}
            {selectedPlugin && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-2 sm:p-4 z-50">
                    <div className="bg-surface rounded-lg w-full h-[95vh] sm:h-[90vh] max-w-5xl flex flex-col">
                        <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 border-b border-surface-lighter gap-4">
                            <h3 className="text-lg text-primary flex items-center gap-2">
                                <i className="fas fa-puzzle-piece"></i>
                                {getEditorTitle()}
                            </h3>
                            <div className="flex flex-wrap sm:flex-nowrap items-center gap-2">
                                <button
                                    onClick={savePluginContent}
                                    disabled={!isContentModified()}
                                    className="flex-1 sm:flex-none px-4 py-2 bg-chardonnay-500 text-white rounded 
                                        hover:bg-chardonnay-600 disabled:opacity-50 
                                        disabled:cursor-not-allowed transition-colors"
                                >
                                    Save
                                </button>
                                <button
                                    onClick={handleCopyToClipboard}
                                    className="flex-1 sm:flex-none px-4 py-2 bg-neutral-600 text-white rounded 
                                        hover:bg-neutral-700 transition-colors"
                                    title="Copy to clipboard"
                                >
                                    <i className="fas fa-copy"></i>
                                </button>
                                <button
                                    onClick={handleDownload}
                                    className="flex-1 sm:flex-none px-4 py-2 bg-neutral-600 text-white rounded 
                                        hover:bg-neutral-700 transition-colors"
                                    title="Download file"
                                >
                                    <i className="fas fa-download"></i>
                                </button>
                                <button
                                    onClick={() => {
                                        if (isContentModified()) {
                                            if (window.confirm('You have unsaved changes. Are you sure you want to close?')) {
                                                setSelectedPlugin(null);
                                            }
                                        } else {
                                            setSelectedPlugin(null);
                                        }
                                    }}
                                    className="flex-1 sm:flex-none p-2 text-neutral-500 hover:text-neutral-700 transition-colors"
                                >
                                    <i className="fas fa-times"></i>
                                </button>
                            </div>
                        </div>
                        <div className="flex-1 p-2 sm:p-4 overflow-hidden">
                            <div className="h-full flex rounded bg-neutral-800">
                                <div className="w-full overflow-auto scrollbar-thin scrollbar-thumb-neutral-600 
                                    scrollbar-track-neutral-800 hover:scrollbar-thumb-neutral-500">
                                    <div id="codemirror-container" className="h-full" />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

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

window.PluginsPage = PluginsPage; 