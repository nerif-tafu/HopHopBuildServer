from flask import jsonify, request
import os
from dotenv import load_dotenv

# Load both .env files
load_dotenv('.env')  # Load default values
load_dotenv('.env.local', override=True)  # Override with local values

@app.route('/api/config', methods=['GET'])
def get_config():
    # Read both .env files
    default_config = {}
    with open('.env', 'r') as f:
        for line in f:
            if line.strip() and not line.startswith('#'):
                key, value = line.strip().split('=', 1)
                default_config[key] = value

    current_config = {}
    try:
        with open('.env.local', 'r') as f:
            for line in f:
                if line.strip() and not line.startswith('#'):
                    key, value = line.strip().split('=', 1)
                    current_config[key] = value
    except FileNotFoundError:
        current_config = default_config.copy()

    return jsonify({
        'current': current_config,
        'defaults': default_config
    })

@app.route('/api/config', methods=['POST'])
def update_config():
    new_config = request.json
    
    # Write to .env.local
    with open('.env.local', 'w') as f:
        for key, value in new_config.items():
            f.write(f"{key}={value}\n")
    
    return jsonify({'status': 'success'})