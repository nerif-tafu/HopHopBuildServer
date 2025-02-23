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

@app.route('/api/server/control', methods=['POST'])
def server_control():
    """Handle server control commands"""
    try:
        data = request.get_json()
        action = data.get('action')
        
        if action == 'start':
            success, message = start_server()
        elif action == 'stop':
            success, message = stop_server()
        elif action == 'restart':
            success, message = restart_server()
        elif action == 'status':
            success = True
            message = {
                'running': is_server_running(),
                'startup_logs': STARTUP_LOGS[-50:],  # Last 50 lines
                'uptime': None  # TODO: Add uptime tracking
            }
        else:
            return jsonify({'error': 'Invalid action'}), 400
        
        if success:
            return jsonify({'message': message})
        else:
            return jsonify({'error': message}), 500
            
    except Exception as e:
        return jsonify({'error': str(e)}), 500

if __name__ == "__main__":
    run_server() 