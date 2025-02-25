const configValidation = {
    SERVER_NAME: {
        validate: (value) => {
            if (!value) return 'Server name is required';
            if (value.length > 64) return 'Server name must be less than 64 characters';
            return null;
        }
    },
    SERVER_PORT: {
        validate: (value) => {
            if (!value) return 'Port is required';
            const port = Number(value);
            if (isNaN(port)) return 'Must be a number';
            if (port < 1024) return 'Port must be >= 1024';
            if (port > 65535) return 'Port must be <= 65535';
            return null;
        }
    },
    SERVER_QUERY: {
        validate: (value) => {
            if (!value) return 'Query port is required';
            const port = Number(value);
            if (isNaN(port)) return 'Must be a number';
            if (port < 1024) return 'Port must be >= 1024';
            if (port > 65535) return 'Port must be <= 65535';
            return null;
        }
    },
    SERVER_RCON_PORT: {
        validate: (value) => {
            if (!value) return 'RCON port is required';
            const port = Number(value);
            if (isNaN(port)) return 'Must be a number';
            if (port < 1024) return 'Port must be >= 1024';
            if (port > 65535) return 'Port must be <= 65535';
            return null;
        }
    },
    SERVER_RCON_PASS: {
        validate: (value) => {
            if (!value) return 'RCON password is required';
            if (value.length < 8) return 'Password must be at least 8 characters';
            return null;
        }
    },
    RUST_BRANCH: {
        validate: (value) => {
            if (!value) return 'Branch is required';
            const validBranches = [
                'master',
                'staging',
                'aux01',
                'aux02',
                'aux03',
                'edge',
                'preview'
            ];
            if (!validBranches.includes(value)) {
                return 'Invalid branch selected';
            }
            return null;
        }
    },
    RUST_ID: {
        validate: (value) => {
            if (!value) return 'Rust ID is required';
            if (!Number.isInteger(Number(value))) return 'Rust ID must be an integer';
            return null;
        }
    },
    SERVER_MAP_SEED: {
        validate: (value) => {
            if (!value) return 'Map seed is required';
            if (!Number.isInteger(Number(value))) return 'Map seed must be an integer';
            return null;
        }
    },
    SERVER_MAP_SIZE: {
        validate: (value) => {
            if (!value) return 'Map size is required';
            const size = Number(value);
            if (isNaN(size)) return 'Must be a number';
            if (!Number.isInteger(size)) return 'Map size must be an integer';
            if (size < 2000) return 'Map size must be at least 2000';
            if (size > 12000) return 'Map size cannot exceed 12000';
            return null;
        }
    },
    SERVER_MAX_PLAYERS: {
        validate: (value) => {
            if (!value) return 'Max players is required';
            const players = Number(value);
            if (isNaN(players)) return 'Must be a number';
            if (players < 1) return 'Must allow at least 1 player';
            if (players > 500) return 'Max players cannot exceed 500';
            return null;
        }
    },
    SERVER_LEVEL_URL: {
        optional: true,
        validate: (value) => {
            if (!value) return null;
            try {
                new URL(value);
                return null;
            } catch (error) {
                return 'Must be a valid URL';
            }
        }
    }
};

window.configValidation = configValidation; 