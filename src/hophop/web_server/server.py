# Monkey patch must happen before other imports
from gevent import monkey
monkey.patch_all()

from flask import Flask, render_template, jsonify, request
from flask_socketio import SocketIO, emit
import subprocess
import threading
import time
import os
import socket
from typing import Optional
from .rcon_client import RustRCON
import json
from functools import partial
from pathlib import Path
from dotenv import load_dotenv
import psutil
import signal
import re
from datetime import datetime
import shutil
from werkzeug.utils import secure_filename
import select

app = Flask(__name__)
app.config['SECRET_KEY'] = os.getenv('FLASK_SECRET_KEY', 'your-secret-key')
socketio = SocketIO(app)

# Add these variables at the top with other imports
RCON_HOST = os.getenv('RCON_HOST', 'localhost')
RCON_PORT = int(os.getenv('SERVER_RCON_PORT', '28017'))  # Use SERVER_RCON_PORT from .env
RCON_PASSWORD = os.getenv('SERVER_RCON_PASS', 'avoid-unelected-thee')  # Use SERVER_RCON_PASS from .env

# Add this after app initialization
rcon_client = RustRCON(RCON_HOST, RCON_PORT, RCON_PASSWORD)

# Get the project root directory - adjust to look in the main repo directory
ROOT_DIR = Path(__file__).parent.parent.parent.parent  # Added one more .parent to go up one more level

# Debug the actual path
print(f"Project root directory: {ROOT_DIR.absolute()}")  # Add this to see the actual path

# Load both .env files
load_dotenv(ROOT_DIR / '.env')  # Load default values
load_dotenv(ROOT_DIR / '.env.local', override=True)  # Override with local values

# Add these at the top with other globals
RUST_SERVER_SCRIPT = ROOT_DIR / 'hophop-rust-server'
VENV_PATH = ROOT_DIR / 'venv'
SERVER_PROCESS = None
STARTUP_LOGS = []

# Add these paths to your existing paths
SCRIPTS_DIR = os.path.join(ROOT_DIR, "src/hophop/rust_server/scripts")
PLUGINS_DIR = os.path.join(ROOT_DIR, "rust_server/carbon/plugins")

# Ensure directories exist
os.makedirs(SCRIPTS_DIR, exist_ok=True)
os.makedirs(PLUGINS_DIR, exist_ok=True)

def is_port_in_use(port: int) -> bool:
    """Check if a port is already in use"""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        try:
            s.bind(('', port))
            return False
        except socket.error:
            return True

def find_available_port(start_port: int, max_attempts: int = 100) -> int:
    """Find the next available port starting from start_port"""
    port = start_port
    while port < (start_port + max_attempts):
        if not is_port_in_use(port):
            return port
        port += 1
    raise RuntimeError(f"Could not find an available port after {max_attempts} attempts")

def print_server_info(host: str, port: int, workers: int):
    """Print server information in a nice format"""
    server_name = os.getenv('SERVER_NAME', 'HopHop Build server | Main')
    screen_name = server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip()
    
    print("\n" + "="*50)
    print("HopHop Web Console Server")
    print("="*50)
    print(f"\n🌐 Server running at:")
    print(f"   • Local:   http://localhost:{port}")
    print(f"   • Network: http://{host}:{port}")
    print("\n📊 Server Status:")
    print(f"   • Workers:  {workers} (gevent)")
    print(f"   • Screen:   {screen_name}")
    print("\n💡 Tips:")
    print("   • Press Ctrl+C to stop the server")
    print("   • The console will auto-refresh every second")
    print("="*50 + "\n")

def get_screen_output(screen_name):
    """Get and emit screen output to connected clients"""
    last_message = ""
    check_interval = 1  # Check every second
    
    def handle_console(response):
        nonlocal last_message
        try:
            if response:
                messages = json.loads(response)
                # Combine all messages into one string, preserving newlines
                content = "\n".join(msg.get('Message', '') for msg in messages if msg.get('Message'))
                if content and content != last_message:
                    socketio.emit('screen_output', {'data': content})
                    last_message = content
        except Exception as e:
            print(f"Error handling console output: {e}")
    
    while True:
        try:
            if rcon_client.connected:
                # Use console.tail to get the last 128 lines of console output
                rcon_client.send_command('console.tail 128', handle_console)
            else:
                message = 'Waiting for server to start...'
                if message != last_message:
                    socketio.emit('screen_output', {'data': message})
                    last_message = message
        except Exception as e:
            print(f"Error in get_console_output: {e}")
        
        time.sleep(check_interval)

@app.route('/')
def index():
    return render_template('index.html')

@socketio.on('connect')
def handle_connect():
    """Handle client connection"""
    print('Client connected')
    
    # Get initial console history
    if rcon_client.connected:
        rcon_client.send_command('console.tail 128', lambda response: 
            socketio.emit('screen_output', {'data': '\n'.join(
                msg.get('Message', '') 
                for msg in json.loads(response) 
                if msg.get('Message')
            )}) if response else None
        )
    
    # Get initial status
    get_server_status()

@socketio.on('disconnect')
def handle_disconnect():
    print('Client disconnected')

def update_server_status():
    """Update and emit server status"""
    status_data = {
        'status': 'online' if rcon_client.connected else 'offline',
        'players': 'Unknown',
        'max_players': 'Unknown',
        'fps': 'Unknown',
        'entities': 'Unknown',
        'raw': ''
    }
    socketio.emit('server_status', {'data': status_data})

def get_server_status():
    """Get server status via RCON"""
    if not rcon_client.connected:
        update_server_status()
        return
        
    status_data = {
        'status': 'online',
        'players': 'Unknown',
        'max_players': 'Unknown',
        'fps': 'Unknown',
        'entities': 'Unknown',
        'raw': ''
    }
    
    def handle_serverinfo(response):
        try:
            if response:
                data = json.loads(response)
                status_data['players'] = data.get('Players', 'Unknown')
                status_data['max_players'] = data.get('MaxPlayers', 'Unknown')
                status_data['fps'] = data.get('Framerate', 'Unknown')
                status_data['entities'] = data.get('EntityCount', 'Unknown')
                status_data['raw'] = json.dumps(data, indent=2)  # Pretty print the raw data
            socketio.emit('server_status', {'data': status_data})
        except Exception as e:
            print(f"Error handling serverinfo: {e}")
            socketio.emit('server_status', {'data': status_data})
    
    rcon_client.send_command('serverinfo', handle_serverinfo)

@socketio.on('request_status')
def handle_status_request():
    """Handle client requests for server status"""
    get_server_status()

def init_rcon():
    """Initialize RCON connection in a separate thread"""
    def rcon_thread():
        try:
            # Set up connection state change callback
            rcon_client.on_state_change = update_server_status
            rcon_client.connect()
        except Exception as e:
            print(f"Failed to connect to RCON: {e}")
            update_server_status()
    
    thread = threading.Thread(target=rcon_thread, daemon=True)
    thread.start()
    return thread

def run_server(bind: Optional[str] = "0.0.0.0:5000", 
              workers: int = 1,
              timeout: int = 30):
    """Main entry point for the web server"""
    host, port_str = bind.split(':')
    initial_port = int(port_str)
    
    try:
        port = find_available_port(initial_port)
        if port != initial_port:
            print(f"Port {initial_port} was in use, using port {port} instead")
    except RuntimeError as e:
        print(f"Error: {e}")
        exit(1)
    
    # Get server name and generate screen name the same way rust server does
    server_name = os.getenv('SERVER_NAME', 'HopHop Build server | Main')
    screen_name = server_name.lower().replace(" ", "_").replace("|", "").replace("\"", "").strip()
    
    # Start the console output monitoring thread (renamed from screen_thread)
    console_thread = threading.Thread(target=get_screen_output, args=(screen_name,), daemon=True)
    console_thread.start()
    
    # Initialize RCON in a separate thread
    rcon_thread = init_rcon()
    
    # Start periodic status updates
    def status_updater():
        while True:
            if rcon_client.connected:
                get_server_status()
            time.sleep(30)  # Update every 30 seconds
    
    status_thread = threading.Thread(target=status_updater, daemon=True)
    status_thread.start()
    
    # Print server information
    print_server_info(host, port, workers)
    
    # Run with Flask-SocketIO's server instead of Gunicorn
    socketio.run(app, host=host, port=port, debug=True, allow_unsafe_werkzeug=True)

@app.route('/api/config', methods=['GET'])
def get_config():
    print("Config endpoint called")
    
    # Read both .env files
    default_config = {}
    env_path = ROOT_DIR / '.env'
    print(f"Looking for .env at: {env_path.absolute()}")  # Use absolute path in log
    if env_path.exists():
        print(".env file found")
        with open(env_path, 'r') as f:
            for line in f:
                if line.strip() and not line.startswith('#'):
                    try:
                        key, value = line.strip().split('=', 1)
                        default_config[key.strip()] = value.strip()  # Added strip() to clean up values
                    except ValueError:
                        continue

    current_config = {}
    env_local_path = ROOT_DIR / '.env.local'
    print(f"Looking for .env.local at: {env_local_path.absolute()}")  # Use absolute path in log
    if env_local_path.exists():
        print(".env.local file found")
        with open(env_local_path, 'r') as f:
            for line in f:
                if line.strip() and not line.startswith('#'):
                    try:
                        key, value = line.strip().split('=', 1)
                        current_config[key.strip()] = value.strip()  # Added strip() to clean up values
                    except ValueError:
                        continue
    else:
        current_config = default_config.copy()

    print(f"Default config: {default_config}")
    print(f"Current config: {current_config}")
    
    response = {
        'current': current_config,
        'defaults': default_config
    }
    print(f"Sending response: {response}")
    return jsonify(response)

@app.route('/api/config', methods=['POST'])
def update_config():
    try:
        new_config = request.json
        env_local_path = ROOT_DIR / '.env.local'
        
        # Write to .env.local
        with open(env_local_path, 'w') as f:
            for key, value in new_config.items():
                f.write(f"{key}={value}\n")
        
        # Reload environment variables
        load_dotenv(ROOT_DIR / '.env')  # Load default values
        load_dotenv(ROOT_DIR / '.env.local', override=True)  # Override with local values
        
        return jsonify({'status': 'success', 'message': 'Configuration updated successfully'})
    except Exception as e:
        return jsonify({'status': 'error', 'message': str(e)}), 500

@app.route('/api/rcon', methods=['POST'])
def rcon_command():
    try:
        data = request.get_json()
        command = data.get('command')
        print(f"\nRCON command received: '{command}'")

        if not command:
            print("No command provided")
            return jsonify({'error': 'No command provided'}), 400

        if not rcon_client.connected:
            print("RCON not connected")
            return jsonify({'error': 'RCON not connected'}), 503

        # Create an event to wait for the response
        response_event = threading.Event()
        response_data = {'response': None}

        def handle_response(response):
            print(f"Raw RCON response: {response}")
            response_data['response'] = response
            response_event.set()

        # Send command and wait for response
        print("Sending command to RCON client...")
        rcon_client.send_command(command, handle_response)
        
        # Wait for response with timeout
        if response_event.wait(timeout=5.0):
            print(f"Final response to send: {response_data['response']}")
            return jsonify({'response': response_data['response']})
        else:
            print("Command timed out")
            return jsonify({'error': 'Command timed out'}), 504

    except Exception as e:
        print(f"Error in RCON command: {str(e)}")
        return jsonify({'error': str(e)}), 500

def get_server_process():
    """Get the current Rust server process if running"""
    if SERVER_PROCESS and SERVER_PROCESS.poll() is None:
        return SERVER_PROCESS
    return None

def is_server_running():
    """Check if the server process is running"""
    process = get_server_process()
    return process is not None

def start_server():
    """Start the Rust server"""
    global SERVER_PROCESS, STARTUP_LOGS
    
    if is_server_running():
        return False, "Server is already running"
    
    STARTUP_LOGS = []
    try:
        # Unix-style command using bash
        activate_cmd = str(VENV_PATH / 'bin' / 'activate')
        cmd = ['/bin/bash', '-c', f'. "{activate_cmd}" && hophop-rust-server']
        
        # Start the server process
        SERVER_PROCESS = subprocess.Popen(
            cmd,
            shell=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            universal_newlines=True
        )
        
        # Start a thread to monitor the output
        def monitor_output():
            while SERVER_PROCESS and SERVER_PROCESS.poll() is None:
                line = SERVER_PROCESS.stdout.readline()
                if line:
                    STARTUP_LOGS.append(line.strip())
                    socketio.emit('server_control', {
                        'status': 'starting',
                        'message': line.strip()
                    })
            
            # Process ended
            if SERVER_PROCESS and SERVER_PROCESS.returncode != 0:
                socketio.emit('server_control', {
                    'status': 'error',
                    'message': f'Server crashed with code {SERVER_PROCESS.returncode}'
                })
        
        threading.Thread(target=monitor_output, daemon=True).start()
        return True, "Server starting"
        
    except Exception as e:
        return False, f"Failed to start server: {str(e)}"

def stop_server():
    """Stop the Rust server"""
    if not is_server_running():
        return False, "Server is not running"
    
    try:
        # Try graceful shutdown first
        if rcon_client.connected:
            rcon_client.send_command('quit')
            time.sleep(2)  # Give it a moment to shut down
        
        # Force kill if still running
        process = get_server_process()
        if process and process.poll() is None:
            if os.name == 'nt':
                process.terminate()
            else:
                process.send_signal(signal.SIGTERM)
            
            # Wait for process to end
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()  # Force kill if it doesn't respond
        
        return True, "Server stopped"
    except Exception as e:
        return False, f"Failed to stop server: {str(e)}"

def restart_server():
    """Restart the Rust server"""
    stop_success, stop_msg = stop_server()
    if not stop_success:
        return False, f"Failed to stop server: {stop_msg}"
    
    time.sleep(2)  # Give it a moment before starting again
    
    start_success, start_msg = start_server()
    if not start_success:
        return False, f"Failed to start server: {start_msg}"
    
    return True, "Server restarting"

def control_service(action):
    """Control the rust server systemd service"""
    try:
        if action not in ['start', 'stop', 'restart', 'enable', 'disable']:
            return False, "Invalid action"
            
        result = subprocess.run(
            ['sudo', 'systemctl', action, 'hophop-rust-server'],
            capture_output=True,
            text=True
        )
        
        if result.returncode == 0:
            return True, f"Service {action} command sent successfully"
        else:
            return False, f"Service {action} failed: {result.stderr}"
    except Exception as e:
        return False, f"Failed to {action} service: {str(e)}"

def get_service_status():
    """Get detailed status of the rust server systemd service"""
    try:
        # Get service status
        result = subprocess.run(
            ['systemctl', 'status', 'hophop-rust-server'],
            capture_output=True,
            text=True
        )
        
        # Get enabled status
        enabled_result = subprocess.run(
            ['systemctl', 'is-enabled', 'hophop-rust-server'],
            capture_output=True,
            text=True
        )
        is_enabled = enabled_result.returncode == 0
        
        # Parse status
        status = 'stopped'
        if 'Active: active (running)' in result.stdout:
            status = 'running'
        elif 'Active: activating' in result.stdout:
            status = 'starting'
        
        # Get last 50 lines of logs
        log_result = subprocess.run(
            ['journalctl', '-u', 'hophop-rust-server', '-n', '50', '--no-pager'],
            capture_output=True,
            text=True
        )
        
        return {
            'status': status,
            'logs': log_result.stdout.splitlines(),
            'uptime': extract_uptime(result.stdout),
            'enabled': is_enabled
        }
    except Exception as e:
        print(f"Error getting service status: {e}")
        return {
            'status': 'unknown',
            'logs': [],
            'uptime': None,
            'enabled': False
        }

def extract_uptime(status_output):
    """Extract service uptime from systemctl status output"""
    match = re.search(r'Active: active \(running\) since (.*?);', status_output)
    if match:
        try:
            start_time = datetime.strptime(match.group(1), '%a %Y-%m-%d %H:%M:%S %Z')
            return (datetime.now() - start_time).total_seconds()
        except:
            return None
    return None

@app.route('/api/server/control', methods=['POST'])
def server_control():
    """Handle server control commands"""
    try:
        data = request.get_json()
        action = data.get('action')
        
        if action in ['start', 'stop', 'restart', 'enable', 'disable']:
            success, message = control_service(action)
        elif action == 'status':
            status = get_service_status()
            return jsonify({
                'message': {
                    'running': status['status'] == 'running',
                    'status': status['status'],
                    'startup_logs': status['logs'],
                    'uptime': status['uptime'],
                    'enabled': status['enabled']
                }
            })
        else:
            return jsonify({'error': 'Invalid action'}), 400
        
        if success:
            return jsonify({'message': message})
        else:
            return jsonify({'error': message}), 500
            
    except Exception as e:
        return jsonify({'error': str(e)}), 500

def get_plugin_status(plugin_name):
    """Check if a plugin is active (exists in plugins directory)"""
    return os.path.exists(os.path.join(PLUGINS_DIR, plugin_name))

@app.route('/api/plugins', methods=['GET'])
def list_plugins():
    """List all available plugins and their status"""
    try:
        base_path = os.path.join(os.path.expanduser('~'), 'HopHopBuildServer')
        scripts_dir = os.path.join(base_path, 'src/hophop/rust_server/scripts')
        config_dir = os.path.join(base_path, 'rust_server/carbon/configs')
        data_dir = os.path.join(base_path, 'rust_server/carbon/data')
        lang_dir = os.path.join(base_path, 'rust_server/carbon/lang/en')

        plugins = []
        for file in os.listdir(scripts_dir):
            if file.endswith('.cs'):
                plugin_name = file[:-3]  # Remove .cs extension
                plugins.append({
                    'name': plugin_name,
                    'active': get_plugin_status(file),
                    'hasConfig': os.path.exists(os.path.join(config_dir, f'{plugin_name}.json')),
                    'hasData': os.path.exists(os.path.join(data_dir, f'{plugin_name}.json')),
                    'hasLang': os.path.exists(os.path.join(lang_dir, f'{plugin_name}.json'))
                })
        return jsonify({'plugins': plugins})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/plugins/toggle', methods=['POST'])
def toggle_plugin():
    """Toggle plugin active state"""
    try:
        data = request.json
        name = data.get('name')
        activate = data.get('activate', False)
        
        if not name:
            return jsonify({'error': 'Plugin name is required'})

        base_path = os.path.join(os.path.expanduser('~'), 'HopHopBuildServer')
        source_path = os.path.join(base_path, 'src/hophop/rust_server/scripts', f'{name}.cs')
        target_path = os.path.join(base_path, 'rust_server/carbon/plugins', f'{name}.cs')
        
        if not os.path.exists(source_path):
            return jsonify({'error': 'Plugin not found'})

        try:
            if activate:
                shutil.copy2(source_path, target_path)
                message = 'Plugin activated'
            else:
                if os.path.exists(target_path):
                    os.remove(target_path)
                message = 'Plugin deactivated'
            
            return jsonify({
                'message': message,
                'active': activate
            })
        except Exception as e:
            return jsonify({'error': f'Failed to toggle plugin: {str(e)}'})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/plugins/delete', methods=['POST'])
def delete_plugin():
    """Delete a plugin and its associated files"""
    try:
        data = request.json
        name = data.get('name')
        
        if not name:
            return jsonify({'error': 'Plugin name is required'})

        base_path = os.path.join(os.path.expanduser('~'), 'HopHopBuildServer')
        file_paths = [
            os.path.join(base_path, 'src/hophop/rust_server/scripts', f'{name}.cs'),
            os.path.join(base_path, 'rust_server/carbon/plugins', f'{name}.cs'),
            os.path.join(base_path, 'rust_server/carbon/configs', f'{name}.json'),
            os.path.join(base_path, 'rust_server/carbon/data', f'{name}.json'),
            os.path.join(base_path, 'rust_server/carbon/lang/en', f'{name}.json')
        ]

        for path in file_paths:
            if os.path.exists(path):
                try:
                    os.remove(path)
                except Exception as e:
                    return jsonify({'error': f'Failed to delete {path}: {str(e)}'})

        return jsonify({'status': 'success'})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/plugins/upload', methods=['POST'])
def upload_plugin():
    """Upload a new plugin file"""
    try:
        if 'file' not in request.files:
            return jsonify({'error': 'No file provided'})
        
        file = request.files['file']
        if file.filename == '':
            return jsonify({'error': 'No file selected'})
        
        if not file.filename.endswith('.cs'):
            return jsonify({'error': 'Only .cs files are allowed'})
        
        base_path = os.path.join(os.path.expanduser('~'), 'HopHopBuildServer')
        filename = secure_filename(file.filename)
        file_path = os.path.join(base_path, 'src/hophop/rust_server/scripts', filename)
        
        file.save(file_path)
        plugin_name = filename[:-3]  # Remove .cs extension
        
        return jsonify({
            'message': 'Plugin uploaded successfully',
            'plugin': {
                'name': plugin_name,
                'active': False,
                'hasConfig': False,
                'hasData': False,
                'hasLang': False
            }
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/plugins/<plugin_name>/<file_type>')
def get_plugin_content(plugin_name, file_type):
    """Get content of a specific plugin file"""
    try:
        base_path = os.path.join(os.path.expanduser('~'), 'HopHopBuildServer')
        file_paths = {
            'code': os.path.join(base_path, 'src/hophop/rust_server/scripts', f'{plugin_name}.cs'),
            'config': os.path.join(base_path, 'rust_server/carbon/configs', f'{plugin_name}.json'),
            'data': os.path.join(base_path, 'rust_server/carbon/data', f'{plugin_name}.json'),
            'lang': os.path.join(base_path, 'rust_server/carbon/lang/en', f'{plugin_name}.json')
        }

        if file_type not in file_paths:
            return jsonify({'error': 'Invalid file type'}), 400

        file_path = file_paths[file_type]
        try:
            with open(file_path, 'r') as f:
                content = f.read()
            return jsonify({'content': content})
        except FileNotFoundError:
            # Return empty content if file doesn't exist
            if file_type in ['config', 'data', 'lang']:
                return jsonify({'content': '{}\n'})  # Default JSON content
            return jsonify({'content': ''})  # Empty content for code files
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@socketio.on('server_control_status')
def handle_server_status():
    """Send server control status updates through WebSocket"""
    try:
        status = get_server_status()  # Your existing status fetching logic
        socketio.emit('server_control_status', status)
    except Exception as e:
        socketio.emit('server_control_status', {'error': str(e)})

def emit_server_log(log_line):
    """Emit server log updates in real-time"""
    try:
        socketio.emit('server_control_logs', {'logs': log_line})
    except Exception as e:
        socketio.emit('server_control_status', {'error': str(e)})

def monitor_journal():
    """Monitor systemd journal in real-time and emit updates"""
    try:
        # Start journalctl in follow mode
        process = subprocess.Popen(
            ['journalctl', '-u', 'hophop-rust-server', '-f', '-n', '0', '--no-pager'],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            universal_newlines=True
        )

        while True:
            # Use select to check for new output without blocking
            if select.select([process.stdout], [], [], 1)[0]:
                line = process.stdout.readline()
                if line:
                    emit_server_log(line.strip())
            
            # Check if process is still alive
            if process.poll() is not None:
                break

    except Exception as e:
        print(f"Error monitoring journal: {e}")

# Start the journal monitor in a separate thread
journal_thread = threading.Thread(target=monitor_journal, daemon=True)
journal_thread.start()

if __name__ == "__main__":
    run_server() 